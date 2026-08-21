using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record ResourcePendingResolutionDraft(
    string ResolutionMode,
    string SessionId,
    string AcceptedRequestId,
    int RequestTurn,
    string EventRef,
    string EffectId,
    ResourcePendingAuthorityBinding EffectAuthority,
    JsonObject Source,
    ResourcePendingAuthorityBinding SourceAuthority,
    JsonObject Target,
    ResourcePendingAuthorityBinding TargetAuthority,
    string TriggerId,
    ResourceCoordinate Coordinate,
    ResourcePendingAuthorityBinding ResourceAuthority,
    ResourceOperation Operation,
    decimal MinimumAmount,
    decimal MaximumAmount,
    string SourceAuthorityFingerprint,
    string PolicyFingerprint,
    string FullTurnFingerprint,
    string SafeSourceLabel,
    string SafeTargetLabel,
    string SafeResourceLabel,
    string SafeOperationLabel);

internal sealed record ResourcePendingAuthorityBinding(
    string BindingKind,
    string AuthorityId);

internal sealed record ResourcePendingResolutionContext(
    string SessionId,
    string AcceptedRequestId,
    int Turn,
    string FullTurnFingerprint);

internal sealed class ResourcePendingRequest
{
    private readonly JsonObject _source;
    private readonly JsonObject _target;
    private readonly string[] _requiredCompanions;

    internal ResourcePendingRequest(
        string requestId,
        string sessionId,
        string acceptedRequestId,
        int requestTurn,
        string eventRef,
        string effectId,
        ResourcePendingAuthorityBinding effectAuthority,
        JsonObject source,
        ResourcePendingAuthorityBinding sourceAuthority,
        JsonObject target,
        ResourcePendingAuthorityBinding targetAuthority,
        string triggerId,
        ResourceCoordinate coordinate,
        ResourcePendingAuthorityBinding resourceAuthority,
        ResourceOperation operation,
        decimal minimumAmount,
        decimal maximumAmount,
        string sourceAuthorityFingerprint,
        string policyFingerprint,
        string fullTurnFingerprint,
        IReadOnlyList<string> requiredCompanions,
        string createdAtUtc,
        string replayFingerprint,
        string safeSourceLabel,
        string safeTargetLabel,
        string safeResourceLabel,
        string safeOperationLabel)
    {
        RequestId = requestId;
        SessionId = sessionId;
        AcceptedRequestId = acceptedRequestId;
        RequestTurn = requestTurn;
        EventRef = eventRef;
        EffectId = effectId;
        EffectAuthority = effectAuthority with { };
        _source = source.DeepClone().AsObject();
        SourceAuthority = sourceAuthority with { };
        _target = target.DeepClone().AsObject();
        TargetAuthority = targetAuthority with { };
        TriggerId = triggerId;
        Coordinate = coordinate with { };
        ResourceAuthority = resourceAuthority with { };
        Operation = operation;
        MinimumAmount = minimumAmount;
        MaximumAmount = maximumAmount;
        SourceAuthorityFingerprint = sourceAuthorityFingerprint;
        PolicyFingerprint = policyFingerprint;
        FullTurnFingerprint = fullTurnFingerprint;
        _requiredCompanions = requiredCompanions.ToArray();
        CreatedAtUtc = createdAtUtc;
        ReplayFingerprint = replayFingerprint;
        SafeSourceLabel = safeSourceLabel;
        SafeTargetLabel = safeTargetLabel;
        SafeResourceLabel = safeResourceLabel;
        SafeOperationLabel = safeOperationLabel;
    }

    internal string RequestId { get; }
    internal string SessionId { get; }
    internal string AcceptedRequestId { get; }
    internal int RequestTurn { get; }
    internal string EventRef { get; }
    internal string EffectId { get; }
    internal ResourcePendingAuthorityBinding EffectAuthority { get; }
    internal JsonObject Source => _source.DeepClone().AsObject();
    internal ResourcePendingAuthorityBinding SourceAuthority { get; }
    internal JsonObject Target => _target.DeepClone().AsObject();
    internal ResourcePendingAuthorityBinding TargetAuthority { get; }
    internal string TriggerId { get; }
    internal ResourceCoordinate Coordinate { get; }
    internal ResourcePendingAuthorityBinding ResourceAuthority { get; }
    internal ResourceOperation Operation { get; }
    internal decimal MinimumAmount { get; }
    internal decimal MaximumAmount { get; }
    internal string SourceAuthorityFingerprint { get; }
    internal string PolicyFingerprint { get; }
    internal string FullTurnFingerprint { get; }
    internal IReadOnlyList<string> RequiredCompanions =>
        Array.AsReadOnly(_requiredCompanions.ToArray());
    internal string CreatedAtUtc { get; }
    internal string ReplayFingerprint { get; }
    internal string State => "pending";
    internal string SafeSourceLabel { get; }
    internal string SafeTargetLabel { get; }
    internal string SafeResourceLabel { get; }
    internal string SafeOperationLabel { get; }

    internal ResourcePendingRequest Clone() =>
        new(
            RequestId,
            SessionId,
            AcceptedRequestId,
            RequestTurn,
            EventRef,
            EffectId,
            EffectAuthority,
            _source,
            SourceAuthority,
            _target,
            TargetAuthority,
            TriggerId,
            Coordinate,
            ResourceAuthority,
            Operation,
            MinimumAmount,
            MaximumAmount,
            SourceAuthorityFingerprint,
            PolicyFingerprint,
            FullTurnFingerprint,
            _requiredCompanions,
            CreatedAtUtc,
            ReplayFingerprint,
            SafeSourceLabel,
            SafeTargetLabel,
            SafeResourceLabel,
            SafeOperationLabel);
}

internal sealed record ResourcePendingTerminalReceipt(
    string RequestId,
    string SessionId,
    string AcceptedRequestId,
    int RequestTurn,
    string EventRef,
    string FullTurnFingerprint,
    string RequestReplayFingerprint,
    string ResultKind,
    decimal? Amount,
    string Reason,
    string ReceiptFingerprint,
    int ResolvedAtTurn,
    string State);

internal sealed record ResourcePendingResolutionStateResult(
    ResourcePendingResolutionState? State,
    IReadOnlyList<ValidationIssue> Issues,
    bool IsMissing = false)
{
    internal bool IsValid => State != null && Issues.Count == 0;
}

internal sealed record ResourcePendingResolutionCreationResult(
    ResourcePendingResolutionState? State,
    JsonObject? SafeGmPacket,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => State != null && SafeGmPacket != null && Issues.Count == 0;
}

internal sealed record ResourcePendingResolutionResult(
    ResourcePendingResolutionState? StateAfterImage,
    IReadOnlyList<ResourceMutationSourceExport> SourceExports,
    IReadOnlyList<ResourceMutationIntent> Mutations,
    IReadOnlyList<ResourcePendingTerminalReceipt> ReplayedTerminalReceipts,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => StateAfterImage != null && Issues.Count == 0;
}

internal sealed class ResourcePendingResolutionState
{
    internal const string PendingPath =
        "game_state/control/pending_effect_resolutions.json";
    internal const int SchemaVersion = 1;
    internal const int MaxRequestsPerTurn = 64;

    private static readonly HashSet<string> RootFields = Set(
        "schemaVersion", "sessionId", "requests", "terminalReceipts");
    private static readonly HashSet<string> RequestFields = Set(
        "requestId", "sessionId", "acceptedRequestId", "requestTurn", "eventRef",
        "effectId", "effectAuthority", "source", "sourceAuthority", "target",
        "targetAuthority", "triggerId", "coordinate", "resourceAuthority", "operation",
        "allowedResults", "sourceAuthorityFingerprint", "policyFingerprint",
        "fullTurnFingerprint", "requiredCompanions", "fullTurnResubmissionRequired",
        "state", "createdAtUtc", "replayFingerprint", "projection");
    private static readonly HashSet<string> SourceFields = Set(
        "kind", "sourceId", "definitionKey");
    private static readonly HashSet<string> TargetFields = Set("kind", "targetId");
    private static readonly HashSet<string> AuthorityBindingFields = Set(
        "bindingKind", "authorityId");
    private static readonly HashSet<string> CoordinateFields = Set(
        "realm", "ownerKind", "resourceOwnerId", "resourceKey");
    private static readonly HashSet<string> AllowedResultFields = Set(
        "resultKind", "operation", "minimumAmount", "maximumAmount");
    private static readonly HashSet<string> ProjectionFields = Set(
        "sourceLabel", "targetLabel", "resourceLabel", "operationLabel");
    private static readonly HashSet<string> TerminalFields = Set(
        "requestId", "sessionId", "acceptedRequestId", "requestTurn", "eventRef",
        "fullTurnFingerprint", "requestReplayFingerprint", "resultKind",
        "amount", "reason", "receiptFingerprint", "resolvedAtTurn", "state");
    private static readonly HashSet<string> ReceiptFields = Set(
        "requestId", "resultKind", "amount", "reason");
    private static readonly HashSet<string> AuthorityBindingKinds = Set(
        "permanent", "same_turn_ref", "accepted_application");

    private readonly ResourcePendingRequest[] _requests;
    private readonly ResourcePendingTerminalReceipt[] _terminalReceipts;

    private ResourcePendingResolutionState(
        string sessionId,
        IReadOnlyList<ResourcePendingRequest> requests,
        IReadOnlyList<ResourcePendingTerminalReceipt> terminalReceipts)
    {
        SessionId = sessionId;
        _requests = requests
            .OrderBy(static request => request.RequestId, StringComparer.Ordinal)
            .Select(static request => request.Clone())
            .ToArray();
        _terminalReceipts = terminalReceipts
            .OrderBy(static receipt => receipt.RequestTurn)
            .ThenBy(static receipt => receipt.RequestId, StringComparer.Ordinal)
            .Select(static receipt => receipt with { })
            .ToArray();
        Fingerprint = Hash("resource-pending-state-v1", ToCanonicalRoot());
    }

    internal string SessionId { get; }

    internal IReadOnlyList<ResourcePendingRequest> Requests =>
        new ReadOnlyCollection<ResourcePendingRequest>(
            _requests.Select(static request => request.Clone()).ToArray());

    internal IReadOnlyList<ResourcePendingTerminalReceipt> TerminalReceipts =>
        Array.AsReadOnly(
            _terminalReceipts.Select(static receipt => receipt with { }).ToArray());

    internal string Fingerprint { get; }

    internal static ResourcePendingResolutionCreationResult CreatePending(
        string? canonicalJson,
        IReadOnlyList<ResourcePendingResolutionDraft> drafts,
        ResourceDefinitionCatalog definitions,
        Func<string> allocateRequestId,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(allocateRequestId);

        var issues = new List<ValidationIssue>();
        if (drafts.Count == 0)
        {
            Add(
                issues,
                PendingPath + ".requests",
                "resource_pending_request_missing",
                "at least one bounded resource request",
                "empty");
            return FailedCreation(issues);
        }
        if (drafts.Count > MaxRequestsPerTurn)
        {
            Add(
                issues,
                PendingPath + ".requests",
                "resource_pending_request_limit_exceeded",
                $"at most {MaxRequestsPerTurn} requests in one accepted turn",
                drafts.Count.ToString(CultureInfo.InvariantCulture));
            return FailedCreation(issues);
        }

        ResourcePendingResolutionState? previous = null;
        if (canonicalJson != null)
        {
            var parsed = ParseCanonical(
                canonicalJson,
                definitions,
                allowMissingPristine: false);
            issues.AddRange(parsed.Issues);
            previous = parsed.State;
            if (issues.Count != 0 || previous == null)
                return FailedCreation(issues);
        }

        var sessionId = drafts[0].SessionId;
        if (previous != null &&
            !string.Equals(previous.SessionId, sessionId, StringComparison.Ordinal))
        {
            Add(
                issues,
                PendingPath + ".sessionId",
                "resource_pending_session_conflict",
                previous.SessionId,
                sessionId);
        }
        if (previous is { _requests.Length: > 0 })
        {
            Add(
                issues,
                PendingPath + ".requests",
                "resource_pending_request_already_active",
                "resolve the exact active pending batch before creating another",
                previous._requests.Length.ToString(CultureInfo.InvariantCulture));
        }

        foreach (var draft in drafts)
            ValidateDraft(draft, definitions, sessionId, issues);
        if (issues.Count != 0)
            return FailedCreation(issues);

        var requestIds = new string[drafts.Count];
        var exactIds = new HashSet<string>(StringComparer.Ordinal);
        var foldedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < requestIds.Length; index++)
        {
            var requestId = allocateRequestId();
            requestIds[index] = requestId;
            if (!ResourceMaterializationContract.IsExactIdentifier(requestId))
            {
                Add(
                    issues,
                    $"{PendingPath}.requests[{index}].requestId",
                    "resource_pending_request_identity_invalid",
                    "client-owned exact request identity",
                    Describe(requestId));
            }
            else if (!exactIds.Add(requestId) || !foldedIds.Add(requestId))
            {
                Add(
                    issues,
                    $"{PendingPath}.requests[{index}].requestId",
                    "resource_pending_request_identity_confusable",
                    "exact and ordinal-ignore-case unique request identity",
                    requestId);
            }
        }
        if (issues.Count != 0)
            return FailedCreation(issues);

        var createdAt = createdAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        var requests = new List<ResourcePendingRequest>(drafts.Count);
        for (var index = 0; index < drafts.Count; index++)
        {
            var draft = drafts[index];
            var companions = requestIds
                .Where((_, candidateIndex) => candidateIndex != index)
                .OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray();
            var request = CreateRequest(
                requestIds[index],
                draft,
                companions,
                createdAt);
            requests.Add(request);
        }

        var state = new ResourcePendingResolutionState(
            sessionId,
            requests,
            previous?._terminalReceipts ?? Array.Empty<ResourcePendingTerminalReceipt>());
        return new ResourcePendingResolutionCreationResult(
            state,
            state.BuildSafeGmPacket(),
            Array.Empty<ValidationIssue>());
    }

    internal static ResourcePendingResolutionStateResult ParseCanonical(
        string? json,
        ResourceDefinitionCatalog definitions,
        bool allowMissingPristine)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (json == null)
        {
            if (allowMissingPristine)
            {
                return new ResourcePendingResolutionStateResult(
                    null,
                    Array.Empty<ValidationIssue>(),
                    IsMissing: true);
            }

            var missingIssues = new List<ValidationIssue>();
            Add(
                missingIssues,
                PendingPath,
                "resource_pending_root_missing",
                "canonical pending-resolution object root",
                "missing");
            return new ResourcePendingResolutionStateResult(null, missingIssues);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            var malformedIssues = new List<ValidationIssue>();
            Add(
                malformedIssues,
                PendingPath,
                "resource_pending_root_invalid",
                "strict JSON object root",
                "malformed JSON");
            return new ResourcePendingResolutionStateResult(null, malformedIssues);
        }

        using (document)
        {
            var issues = new List<ValidationIssue>();
            ValidateUniqueProperties(document.RootElement, PendingPath, issues);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                Add(
                    issues,
                    PendingPath,
                    "resource_pending_root_invalid",
                    "strict JSON object root",
                    document.RootElement.ValueKind.ToString());
                return new ResourcePendingResolutionStateResult(null, issues);
            }

            ValidateClosedObject(
                document.RootElement,
                PendingPath,
                RootFields,
                "resource_pending_unknown_field",
                issues);
            var schemaVersion = ReadInt(
                document.RootElement,
                "schemaVersion",
                PendingPath,
                issues,
                "resource_pending_schema_invalid");
            if (schemaVersion != SchemaVersion)
            {
                Add(
                    issues,
                    PendingPath + ".schemaVersion",
                    "resource_pending_schema_invalid",
                    SchemaVersion.ToString(CultureInfo.InvariantCulture),
                    schemaVersion?.ToString(CultureInfo.InvariantCulture) ?? "missing");
            }
            var sessionId = ReadExact(
                document.RootElement,
                "sessionId",
                PendingPath,
                issues,
                "resource_pending_session_invalid");
            var requests = ParseRequests(
                document.RootElement,
                definitions,
                sessionId,
                issues);
            var terminals = ParseTerminals(document.RootElement, issues);
            ValidateRequestIdentitySets(requests, terminals, issues);
            ValidateCompanions(requests, issues);
            if (issues.Count != 0 || sessionId == null)
                return new ResourcePendingResolutionStateResult(null, issues);

            return new ResourcePendingResolutionStateResult(
                new ResourcePendingResolutionState(sessionId, requests, terminals),
                Array.Empty<ValidationIssue>());
        }
    }

    internal ResourcePendingResolutionResult Resolve(
        JsonArray receipts,
        ResourcePendingResolutionContext context,
        ResourceDefinitionCatalog definitions)
    {
        ArgumentNullException.ThrowIfNull(receipts);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(definitions);
        var issues = new List<ValidationIssue>();
        ValidateContext(context, issues);
        foreach (var request in _requests)
            ValidateRequestContext(request, context, issues);

        var parsedReceipts = ParseReceipts(receipts, issues);
        var receiptById = parsedReceipts
            .Where(static receipt => receipt.RequestId != null)
            .GroupBy(static receipt => receipt.RequestId!, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
        var pendingById = _requests.ToDictionary(
            static request => request.RequestId,
            StringComparer.Ordinal);
        var terminalById = _terminalReceipts.ToDictionary(
            static receipt => receipt.RequestId,
            StringComparer.Ordinal);

        foreach (var receipt in parsedReceipts)
        {
            if (receipt.RequestId == null)
                continue;
            if (!pendingById.ContainsKey(receipt.RequestId) &&
                !terminalById.ContainsKey(receipt.RequestId))
            {
                Add(
                    issues,
                    receipt.Path + ".requestId",
                    "resource_pending_receipt_extra",
                    "one exact pending or terminal request identity",
                    receipt.RequestId);
            }
        }
        foreach (var request in _requests)
        {
            if (!receiptById.ContainsKey(request.RequestId))
            {
                Add(
                    issues,
                    PendingPath + ".requests[" + request.RequestId + "]",
                    "resource_pending_receipt_missing",
                    "one receipt for every pending request",
                    request.RequestId);
            }
            foreach (var companion in request.RequiredCompanions)
            {
                if (!receiptById.ContainsKey(companion))
                {
                    Add(
                        issues,
                        PendingPath + ".requests[" + request.RequestId + "].requiredCompanions",
                        "resource_pending_companion_missing",
                        "receipt for required companion request " + companion,
                        "missing");
                }
            }
        }

        var replayed = new List<ResourcePendingTerminalReceipt>();
        foreach (var receipt in parsedReceipts)
        {
            if (receipt.RequestId == null ||
                !terminalById.TryGetValue(receipt.RequestId, out var terminal))
            {
                continue;
            }
            if (!string.Equals(terminal.SessionId, context.SessionId, StringComparison.Ordinal) ||
                !string.Equals(
                    terminal.AcceptedRequestId,
                    context.AcceptedRequestId,
                    StringComparison.Ordinal) ||
                terminal.RequestTurn != context.Turn ||
                !string.Equals(
                    terminal.FullTurnFingerprint,
                    context.FullTurnFingerprint,
                    StringComparison.Ordinal))
            {
                Add(
                    issues,
                    receipt.Path,
                    "resource_pending_receipt_replay_stale",
                    "same terminal session/request/turn/full-turn authority",
                    $"{context.SessionId}/{context.AcceptedRequestId}/{context.Turn}/{context.FullTurnFingerprint}");
                continue;
            }
            var fingerprint = ComputeReceiptFingerprint(
                receipt.RequestId,
                receipt.ResultKind,
                receipt.Amount,
                receipt.Reason);
            if (!string.Equals(
                    fingerprint,
                    terminal.ReceiptFingerprint,
                    StringComparison.Ordinal))
            {
                Add(
                    issues,
                    receipt.Path,
                    "resource_pending_receipt_replay_conflict",
                    "exact terminal receipt semantics",
                    fingerprint);
            }
            else
            {
                replayed.Add(terminal with { });
            }
        }

        var mutations = new List<ResourceMutationIntent>();
        var sourceExports = new List<ResourceMutationSourceExport>();
        var newTerminals = new List<ResourcePendingTerminalReceipt>();
        foreach (var request in _requests)
        {
            if (!receiptById.TryGetValue(request.RequestId, out var receipt))
                continue;
            ValidateReceiptAgainstRequest(receipt, request, definitions, issues);
            if (issues.Count != 0)
                continue;
            var fingerprint = ComputeReceiptFingerprint(
                request.RequestId,
                receipt.ResultKind,
                receipt.Amount,
                receipt.Reason);
            newTerminals.Add(new ResourcePendingTerminalReceipt(
                request.RequestId,
                request.SessionId,
                request.AcceptedRequestId,
                request.RequestTurn,
                request.EventRef,
                request.FullTurnFingerprint,
                request.ReplayFingerprint,
                receipt.ResultKind!,
                receipt.Amount,
                receipt.Reason!,
                fingerprint,
                context.Turn,
                "resolved_and_consumed"));
            if (!string.Equals(
                    receipt.ResultKind,
                    "resource_delta",
                    StringComparison.Ordinal))
            {
                continue;
            }

            var sourceFingerprint = Hash(
                "resource-bounded-receipt-source-v1",
                new JsonObject
                {
                    ["requestId"] = request.RequestId,
                    ["requestReplayFingerprint"] = request.ReplayFingerprint,
                    ["receiptFingerprint"] = fingerprint
                });
            sourceExports.Add(new ResourceMutationSourceExport(
                "bounded_receipt",
                request.RequestId,
                sourceFingerprint,
                ResourceMutationSourceState.Active,
                SameTurn: true));
            mutations.Add(new ResourceMutationIntent(
                request.EventRef,
                request.Coordinate,
                receipt.Amount!.Value,
                new ResourceMutationSourceRequest(
                    "bounded_receipt",
                    request.RequestId,
                    request.Operation),
                Array.Empty<ResourceOperationKey>(),
                Array.Empty<ResourceMutationEventRequirement>(),
                request.RequestId));
        }

        if (issues.Count != 0)
        {
            return new ResourcePendingResolutionResult(
                null,
                Array.Empty<ResourceMutationSourceExport>(),
                Array.Empty<ResourceMutationIntent>(),
                Array.Empty<ResourcePendingTerminalReceipt>(),
                issues.ToArray());
        }

        var terminalAfter = _terminalReceipts
            .Concat(newTerminals)
            .OrderBy(static receipt => receipt.RequestTurn)
            .ThenBy(static receipt => receipt.RequestId, StringComparer.Ordinal)
            .ToArray();
        var stateAfter = new ResourcePendingResolutionState(
            SessionId,
            Array.Empty<ResourcePendingRequest>(),
            terminalAfter);
        return new ResourcePendingResolutionResult(
            stateAfter,
            sourceExports.ToArray(),
            mutations.ToArray(),
            replayed.ToArray(),
            Array.Empty<ValidationIssue>());
    }

    internal JsonObject BuildSafeGmPacket()
    {
        var requests = new JsonArray();
        foreach (var request in _requests)
        {
            requests.Add(new JsonObject
            {
                ["requestId"] = request.RequestId,
                ["sourceLabel"] = request.SafeSourceLabel,
                ["targetLabel"] = request.SafeTargetLabel,
                ["resourceLabel"] = request.SafeResourceLabel,
                ["operationLabel"] = request.SafeOperationLabel,
                ["allowedResults"] = new JsonArray(
                    new JsonObject
                    {
                        ["resultKind"] = "narrated_no_state_change"
                    },
                    new JsonObject
                    {
                        ["resultKind"] = "resource_delta",
                        ["amountInstruction"] = string.Create(
                            CultureInfo.InvariantCulture,
                            $"от {request.MinimumAmount} до {request.MaximumAmount}")
                    }),
                ["requiredCompanions"] = new JsonArray(request.RequiredCompanions
                    .Select(static value => (JsonNode)value)
                    .ToArray()),
                ["fullTurnResubmissionRequired"] = true,
                ["instruction"] =
                    "Верните один разрешённый результат и краткую внутриигровую причину; не указывайте техническую цель, ресурс, операцию или состояние."
            });
        }
        return new JsonObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["kind"] = "bounded_resource_resolution",
            ["requests"] = requests
        };
    }

    internal JsonObject ToCanonicalRoot()
    {
        var requests = new JsonArray(_requests
            .Select(static request => (JsonNode)ToCanonicalRequest(request))
            .ToArray());
        var terminals = new JsonArray(_terminalReceipts
            .Select(static receipt => (JsonNode)ToCanonicalTerminal(receipt))
            .ToArray());
        return new JsonObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["sessionId"] = SessionId,
            ["requests"] = requests,
            ["terminalReceipts"] = terminals
        };
    }

    internal string ToCanonicalJson() => ToCanonicalRoot().ToJsonString();

    private static ResourcePendingRequest CreateRequest(
        string requestId,
        ResourcePendingResolutionDraft draft,
        IReadOnlyList<string> companions,
        string createdAtUtc)
    {
        var provisional = new ResourcePendingRequest(
            requestId,
            draft.SessionId,
            draft.AcceptedRequestId,
            draft.RequestTurn,
            draft.EventRef,
            draft.EffectId,
            draft.EffectAuthority,
            draft.Source,
            draft.SourceAuthority,
            draft.Target,
            draft.TargetAuthority,
            draft.TriggerId,
            draft.Coordinate,
            draft.ResourceAuthority,
            draft.Operation,
            draft.MinimumAmount,
            draft.MaximumAmount,
            draft.SourceAuthorityFingerprint,
            draft.PolicyFingerprint,
            draft.FullTurnFingerprint,
            companions,
            createdAtUtc,
            replayFingerprint: string.Empty,
            draft.SafeSourceLabel,
            draft.SafeTargetLabel,
            draft.SafeResourceLabel,
            draft.SafeOperationLabel);
        var replayFingerprint = ComputeRequestReplayFingerprint(provisional);
        return new ResourcePendingRequest(
            requestId,
            draft.SessionId,
            draft.AcceptedRequestId,
            draft.RequestTurn,
            draft.EventRef,
            draft.EffectId,
            draft.EffectAuthority,
            draft.Source,
            draft.SourceAuthority,
            draft.Target,
            draft.TargetAuthority,
            draft.TriggerId,
            draft.Coordinate,
            draft.ResourceAuthority,
            draft.Operation,
            draft.MinimumAmount,
            draft.MaximumAmount,
            draft.SourceAuthorityFingerprint,
            draft.PolicyFingerprint,
            draft.FullTurnFingerprint,
            companions,
            createdAtUtc,
            replayFingerprint,
            draft.SafeSourceLabel,
            draft.SafeTargetLabel,
            draft.SafeResourceLabel,
            draft.SafeOperationLabel);
    }

    private static void ValidateDraft(
        ResourcePendingResolutionDraft draft,
        ResourceDefinitionCatalog definitions,
        string expectedSessionId,
        List<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(draft.Source);
        ArgumentNullException.ThrowIfNull(draft.Target);
        ArgumentNullException.ThrowIfNull(draft.Coordinate);
        ArgumentNullException.ThrowIfNull(draft.EffectAuthority);
        ArgumentNullException.ThrowIfNull(draft.SourceAuthority);
        ArgumentNullException.ThrowIfNull(draft.TargetAuthority);
        ArgumentNullException.ThrowIfNull(draft.ResourceAuthority);
        if (!string.Equals(
                draft.ResolutionMode,
                "bounded_receipt",
                StringComparison.Ordinal))
        {
            Add(
                issues,
                PendingPath + ".resolutionMode",
                "resource_pending_resolution_mode_forbidden",
                "bounded_receipt effect trigger only; deterministic operations never pend",
                Describe(draft.ResolutionMode));
        }
        ValidateExact(draft.SessionId, "sessionId", "resource_pending_session_invalid", issues);
        if (!string.Equals(draft.SessionId, expectedSessionId, StringComparison.Ordinal))
        {
            Add(
                issues,
                PendingPath + ".sessionId",
                "resource_pending_session_conflict",
                expectedSessionId,
                draft.SessionId);
        }
        ValidateExact(
            draft.AcceptedRequestId,
            "acceptedRequestId",
            "resource_pending_turn_request_invalid",
            issues);
        if (draft.RequestTurn <= 0)
        {
            Add(
                issues,
                PendingPath + ".requestTurn",
                "resource_pending_turn_invalid",
                "positive accepted turn",
                draft.RequestTurn.ToString(CultureInfo.InvariantCulture));
        }
        ValidateExact(draft.EventRef, "eventRef", "resource_pending_event_invalid", issues);
        ValidateExact(draft.EffectId, "effectId", "resource_pending_effect_invalid", issues);
        ValidateAuthorityBinding(draft.EffectAuthority, "effectAuthority", issues);
        ValidateExact(draft.TriggerId, "triggerId", "resource_pending_trigger_invalid", issues);
        ValidateSourceTarget(draft.Source, draft.Target, PendingPath, issues);
        ValidateAuthorityBinding(draft.SourceAuthority, "sourceAuthority", issues);
        ValidateAuthorityBinding(draft.TargetAuthority, "targetAuthority", issues);
        ValidateAuthorityBinding(draft.ResourceAuthority, "resourceAuthority", issues);
        ValidateCoordinateAndBounds(
            draft.Coordinate,
            draft.Operation,
            draft.MinimumAmount,
            draft.MaximumAmount,
            definitions,
            PendingPath,
            issues);
        ValidateFingerprint(
            draft.SourceAuthorityFingerprint,
            "sourceAuthorityFingerprint",
            issues);
        ValidateFingerprint(draft.PolicyFingerprint, "policyFingerprint", issues);
        ValidateFingerprint(draft.FullTurnFingerprint, "fullTurnFingerprint", issues);
        ValidateSafeLabel(draft.SafeSourceLabel, "safeSourceLabel", issues);
        ValidateSafeLabel(draft.SafeTargetLabel, "safeTargetLabel", issues);
        ValidateSafeLabel(draft.SafeResourceLabel, "safeResourceLabel", issues);
        ValidateSafeLabel(draft.SafeOperationLabel, "safeOperationLabel", issues);
    }

    private static List<ResourcePendingRequest> ParseRequests(
        JsonElement root,
        ResourceDefinitionCatalog definitions,
        string? rootSessionId,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("requests", out var requestsNode) ||
            requestsNode.ValueKind != JsonValueKind.Array)
        {
            Add(
                issues,
                PendingPath + ".requests",
                "resource_pending_requests_invalid",
                "requests array",
                Describe(root, "requests"));
            return new List<ResourcePendingRequest>();
        }
        if (requestsNode.GetArrayLength() > MaxRequestsPerTurn)
        {
            Add(
                issues,
                PendingPath + ".requests",
                "resource_pending_request_limit_exceeded",
                $"at most {MaxRequestsPerTurn} requests",
                requestsNode.GetArrayLength().ToString(CultureInfo.InvariantCulture));
        }

        var requests = new List<ResourcePendingRequest>();
        var index = 0;
        foreach (var node in requestsNode.EnumerateArray())
        {
            var path = $"{PendingPath}.requests[{index}]";
            index++;
            if (node.ValueKind != JsonValueKind.Object)
            {
                Add(
                    issues,
                    path,
                    "resource_pending_request_invalid",
                    "strict request object",
                    node.ValueKind.ToString());
                continue;
            }
            ValidateClosedObject(
                node,
                path,
                RequestFields,
                "resource_pending_unknown_field",
                issues);
            var requestId = ReadExact(
                node,
                "requestId",
                path,
                issues,
                "resource_pending_request_identity_invalid");
            var sessionId = ReadExact(
                node,
                "sessionId",
                path,
                issues,
                "resource_pending_session_invalid");
            if (sessionId != null && rootSessionId != null &&
                !string.Equals(sessionId, rootSessionId, StringComparison.Ordinal))
            {
                Add(
                    issues,
                    path + ".sessionId",
                    "resource_pending_session_conflict",
                    rootSessionId,
                    sessionId);
            }
            var acceptedRequestId = ReadExact(
                node,
                "acceptedRequestId",
                path,
                issues,
                "resource_pending_turn_request_invalid");
            var requestTurn = ReadInt(
                node,
                "requestTurn",
                path,
                issues,
                "resource_pending_turn_invalid");
            if (requestTurn <= 0)
            {
                Add(
                    issues,
                    path + ".requestTurn",
                    "resource_pending_turn_invalid",
                    "positive accepted turn",
                    requestTurn?.ToString(CultureInfo.InvariantCulture) ?? "missing");
            }
            var eventRef = ReadExact(
                node,
                "eventRef",
                path,
                issues,
                "resource_pending_event_invalid");
            var effectId = ReadExact(
                node,
                "effectId",
                path,
                issues,
                "resource_pending_effect_invalid");
            var effectAuthority = ParseAuthorityBinding(
                node,
                "effectAuthority",
                path,
                issues);
            var triggerId = ReadExact(
                node,
                "triggerId",
                path,
                issues,
                "resource_pending_trigger_invalid");
            var source = ParseStrictIdentityObject(
                node,
                "source",
                path,
                SourceFields,
                new[] { "kind", "sourceId", "definitionKey" },
                issues);
            var sourceAuthority = ParseAuthorityBinding(
                node,
                "sourceAuthority",
                path,
                issues);
            var target = ParseStrictIdentityObject(
                node,
                "target",
                path,
                TargetFields,
                new[] { "kind", "targetId" },
                issues);
            var targetAuthority = ParseAuthorityBinding(
                node,
                "targetAuthority",
                path,
                issues);
            var coordinate = ParseCoordinate(node, path, issues);
            var resourceAuthority = ParseAuthorityBinding(
                node,
                "resourceAuthority",
                path,
                issues);
            var operation = ParseOperation(node, path, issues);
            var (minimum, maximum) = ParseAllowedResults(node, operation, path, issues);
            var sourceFingerprint = ReadFingerprint(
                node,
                "sourceAuthorityFingerprint",
                path,
                issues);
            var policyFingerprint = ReadFingerprint(
                node,
                "policyFingerprint",
                path,
                issues);
            var fullTurnFingerprint = ReadFingerprint(
                node,
                "fullTurnFingerprint",
                path,
                issues);
            var companions = ReadExactStringArray(
                node,
                "requiredCompanions",
                path,
                issues);
            if (!ReadBool(node, "fullTurnResubmissionRequired", path, issues))
            {
                Add(
                    issues,
                    path + ".fullTurnResubmissionRequired",
                    "resource_pending_full_turn_required",
                    "true",
                    Describe(node, "fullTurnResubmissionRequired"));
            }
            var state = ReadExact(
                node,
                "state",
                path,
                issues,
                "resource_pending_state_invalid");
            if (state != null && !string.Equals(state, "pending", StringComparison.Ordinal))
            {
                Add(
                    issues,
                    path + ".state",
                    "resource_pending_state_invalid",
                    "pending",
                    state);
            }
            var createdAtUtc = ReadUtc(node, "createdAtUtc", path, issues);
            var replayFingerprint = ReadFingerprint(
                node,
                "replayFingerprint",
                path,
                issues);
            var projection = ParseProjection(node, path, issues);

            if (coordinate != null && operation.HasValue &&
                minimum.HasValue && maximum.HasValue)
            {
                ValidateCoordinateAndBounds(
                    coordinate,
                    operation.Value,
                    minimum.Value,
                    maximum.Value,
                    definitions,
                    path,
                    issues);
            }
            if (new object?[]
                {
                    requestId, sessionId, acceptedRequestId, requestTurn, eventRef,
                    effectId, effectAuthority, source, sourceAuthority, target,
                    targetAuthority, triggerId, coordinate, resourceAuthority, operation, minimum,
                    maximum, sourceFingerprint, policyFingerprint, fullTurnFingerprint,
                    createdAtUtc, replayFingerprint, projection
                }.Any(static value => value == null))
            {
                continue;
            }

            var request = new ResourcePendingRequest(
                requestId!,
                sessionId!,
                acceptedRequestId!,
                requestTurn!.Value,
                eventRef!,
                effectId!,
                effectAuthority!,
                source!,
                sourceAuthority!,
                target!,
                targetAuthority!,
                triggerId!,
                coordinate!,
                resourceAuthority!,
                operation!.Value,
                minimum!.Value,
                maximum!.Value,
                sourceFingerprint!,
                policyFingerprint!,
                fullTurnFingerprint!,
                companions,
                createdAtUtc!,
                replayFingerprint!,
                projection!.Value.Source,
                projection.Value.Target,
                projection.Value.Resource,
                projection.Value.Operation);
            var expectedReplay = ComputeRequestReplayFingerprint(request);
            if (!string.Equals(
                    replayFingerprint,
                    expectedReplay,
                    StringComparison.Ordinal))
            {
                Add(
                    issues,
                    path + ".replayFingerprint",
                    "resource_pending_replay_fingerprint_mismatch",
                    expectedReplay,
                    replayFingerprint!);
            }
            requests.Add(request);
        }
        return requests;
    }

    private static List<ResourcePendingTerminalReceipt> ParseTerminals(
        JsonElement root,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("terminalReceipts", out var terminalsNode) ||
            terminalsNode.ValueKind != JsonValueKind.Array)
        {
            Add(
                issues,
                PendingPath + ".terminalReceipts",
                "resource_pending_terminal_receipts_invalid",
                "terminalReceipts array",
                Describe(root, "terminalReceipts"));
            return new List<ResourcePendingTerminalReceipt>();
        }
        var terminals = new List<ResourcePendingTerminalReceipt>();
        var index = 0;
        foreach (var node in terminalsNode.EnumerateArray())
        {
            var path = $"{PendingPath}.terminalReceipts[{index}]";
            index++;
            if (node.ValueKind != JsonValueKind.Object)
            {
                Add(
                    issues,
                    path,
                    "resource_pending_terminal_receipt_invalid",
                    "strict terminal receipt object",
                    node.ValueKind.ToString());
                continue;
            }
            ValidateClosedObject(
                node,
                path,
                TerminalFields,
                "resource_pending_unknown_field",
                issues);
            var requestId = ReadExact(
                node,
                "requestId",
                path,
                issues,
                "resource_pending_request_identity_invalid");
            var sessionId = ReadExact(
                node,
                "sessionId",
                path,
                issues,
                "resource_pending_session_invalid");
            var acceptedRequestId = ReadExact(
                node,
                "acceptedRequestId",
                path,
                issues,
                "resource_pending_turn_request_invalid");
            var requestTurn = ReadInt(
                node,
                "requestTurn",
                path,
                issues,
                "resource_pending_turn_invalid");
            var eventRef = ReadExact(
                node,
                "eventRef",
                path,
                issues,
                "resource_pending_event_invalid");
            var fullTurnFingerprint = ReadFingerprint(
                node,
                "fullTurnFingerprint",
                path,
                issues);
            var requestReplayFingerprint = ReadFingerprint(
                node,
                "requestReplayFingerprint",
                path,
                issues);
            var resultKind = ReadExact(
                node,
                "resultKind",
                path,
                issues,
                "resource_pending_receipt_result_invalid");
            decimal? amount = null;
            if (node.TryGetProperty("amount", out var amountNode))
            {
                if (!amountNode.TryGetDecimal(out var parsedAmount))
                {
                    Add(
                        issues,
                        path + ".amount",
                        "resource_pending_receipt_amount_invalid",
                        "exact decimal amount",
                        amountNode.GetRawText());
                }
                else
                {
                    amount = parsedAmount;
                }
            }
            var reason = ReadReason(node, path, issues);
            var receiptFingerprint = ReadFingerprint(
                node,
                "receiptFingerprint",
                path,
                issues);
            var resolvedAtTurn = ReadInt(
                node,
                "resolvedAtTurn",
                path,
                issues,
                "resource_pending_terminal_turn_invalid");
            var state = ReadExact(
                node,
                "state",
                path,
                issues,
                "resource_pending_state_invalid");
            if (state != null &&
                !string.Equals(state, "resolved_and_consumed", StringComparison.Ordinal))
            {
                Add(
                    issues,
                    path + ".state",
                    "resource_pending_state_invalid",
                    "resolved_and_consumed",
                    state);
            }
            if (requestId == null || sessionId == null || acceptedRequestId == null ||
                !requestTurn.HasValue || eventRef == null || resultKind == null ||
                fullTurnFingerprint == null || requestReplayFingerprint == null ||
                reason == null || receiptFingerprint == null ||
                !resolvedAtTurn.HasValue || state == null)
            {
                continue;
            }
            ValidateResultShape(resultKind, amount, path, issues);
            var expectedFingerprint = ComputeReceiptFingerprint(
                requestId,
                resultKind,
                amount,
                reason);
            if (!string.Equals(
                    receiptFingerprint,
                    expectedFingerprint,
                    StringComparison.Ordinal))
            {
                Add(
                    issues,
                    path + ".receiptFingerprint",
                    "resource_pending_receipt_fingerprint_mismatch",
                    expectedFingerprint,
                    receiptFingerprint);
            }
            terminals.Add(new ResourcePendingTerminalReceipt(
                requestId,
                sessionId,
                acceptedRequestId,
                requestTurn.Value,
                eventRef,
                fullTurnFingerprint,
                requestReplayFingerprint,
                resultKind,
                amount,
                reason,
                receiptFingerprint,
                resolvedAtTurn.Value,
                state));
        }
        return terminals;
    }

    private static IReadOnlyList<ParsedReceipt> ParseReceipts(
        JsonArray receipts,
        List<ValidationIssue> issues)
    {
        var result = new List<ParsedReceipt>();
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var folded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < receipts.Count; index++)
        {
            var path = $"effectResolutionReceipts[{index}]";
            if (receipts[index] is not JsonObject receipt)
            {
                Add(
                    issues,
                    path,
                    "resource_pending_receipt_invalid",
                    "strict receipt object",
                    Describe(receipts[index]));
                continue;
            }
            foreach (var property in receipt)
            {
                if (!ReceiptFields.Contains(property.Key))
                {
                    Add(
                        issues,
                        path + "." + property.Key,
                        "resource_pending_receipt_unknown_field",
                        "requestId, resultKind, optional amount, and reason only",
                        property.Key);
                }
            }
            var requestId = ReadExact(
                receipt,
                "requestId",
                path,
                issues,
                "resource_pending_request_identity_invalid");
            if (requestId != null &&
                (!exact.Add(requestId) || !folded.Add(requestId)))
            {
                Add(
                    issues,
                    path + ".requestId",
                    "resource_pending_receipt_duplicate",
                    "one exact receipt per request",
                    requestId);
            }
            var resultKind = ReadExact(
                receipt,
                "resultKind",
                path,
                issues,
                "resource_pending_receipt_result_invalid");
            decimal? amount = null;
            if (receipt.TryGetPropertyValue("amount", out var amountNode))
            {
                if (!TryReadDecimal(amountNode, out var parsedAmount))
                {
                    Add(
                        issues,
                        path + ".amount",
                        "resource_pending_receipt_amount_invalid",
                        "exact decimal amount",
                        Describe(amountNode));
                }
                else
                {
                    amount = parsedAmount;
                }
            }
            var reason = ReadReason(receipt, path, issues);
            if (resultKind != null)
                ValidateResultShape(resultKind, amount, path, issues);
            result.Add(new ParsedReceipt(path, requestId, resultKind, amount, reason));
        }
        return result;
    }

    private static void ValidateReceiptAgainstRequest(
        ParsedReceipt receipt,
        ResourcePendingRequest request,
        ResourceDefinitionCatalog definitions,
        List<ValidationIssue> issues)
    {
        if (receipt.ResultKind == null || receipt.Reason == null)
            return;
        if (receipt.ResultKind is not ("narrated_no_state_change" or "resource_delta"))
        {
            Add(
                issues,
                receipt.Path + ".resultKind",
                "resource_pending_receipt_result_invalid",
                "narrated_no_state_change or resource_delta",
                receipt.ResultKind);
            return;
        }
        if (!string.Equals(receipt.ResultKind, "resource_delta", StringComparison.Ordinal))
            return;
        if (!receipt.Amount.HasValue)
            return;
        if (receipt.Amount.Value < request.MinimumAmount ||
            receipt.Amount.Value > request.MaximumAmount)
        {
            Add(
                issues,
                receipt.Path + ".amount",
                "resource_pending_receipt_out_of_bounds",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"amount from {request.MinimumAmount} through {request.MaximumAmount}"),
                receipt.Amount.Value.ToString(CultureInfo.InvariantCulture));
            return;
        }
        if (!definitions.TryResolveExact(request.Coordinate.ResourceKey, out var definition) ||
            definition == null ||
            !ResourceMaterializationContract.IsQuantumAligned(
                receipt.Amount.Value,
                0m,
                definition.Quantum))
        {
            Add(
                issues,
                receipt.Path + ".amount",
                "resource_pending_receipt_quantum_invalid",
                definition == null
                    ? "amount aligned to one exact sealed resource definition"
                    : "amount aligned to quantum " +
                      definition.Quantum.ToString(CultureInfo.InvariantCulture),
                receipt.Amount.Value.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void ValidateResultShape(
        string resultKind,
        decimal? amount,
        string path,
        List<ValidationIssue> issues)
    {
        if (string.Equals(resultKind, "narrated_no_state_change", StringComparison.Ordinal))
        {
            if (amount.HasValue)
            {
                Add(
                    issues,
                    path + ".amount",
                    "resource_pending_receipt_amount_forbidden",
                    "amount absent for narrated_no_state_change",
                    amount.Value.ToString(CultureInfo.InvariantCulture));
            }
            return;
        }
        if (string.Equals(resultKind, "resource_delta", StringComparison.Ordinal))
        {
            if (!amount.HasValue)
            {
                Add(
                    issues,
                    path + ".amount",
                    "resource_pending_receipt_amount_invalid",
                    "exact bounded decimal amount",
                    "missing");
            }
            return;
        }
        Add(
            issues,
            path + ".resultKind",
            "resource_pending_receipt_result_invalid",
            "narrated_no_state_change or resource_delta",
            resultKind);
    }

    private static void ValidateContext(
        ResourcePendingResolutionContext context,
        List<ValidationIssue> issues)
    {
        ValidateExact(context.SessionId, "sessionId", "resource_pending_session_stale", issues);
        ValidateExact(
            context.AcceptedRequestId,
            "acceptedRequestId",
            "resource_pending_turn_request_stale",
            issues);
        if (context.Turn <= 0)
        {
            Add(
                issues,
                "pending.context.turn",
                "resource_pending_turn_stale",
                "positive exact resubmitted turn",
                context.Turn.ToString(CultureInfo.InvariantCulture));
        }
        if (!ResourceMaterializationContract.IsAuthorityFingerprint(
                context.FullTurnFingerprint))
        {
            Add(
                issues,
                "pending.context.fullTurnFingerprint",
                "resource_pending_full_turn_stale",
                "lowercase SHA-256 full-turn fingerprint",
                Describe(context.FullTurnFingerprint));
        }
    }

    private void ValidateRequestContext(
        ResourcePendingRequest request,
        ResourcePendingResolutionContext context,
        List<ValidationIssue> issues)
    {
        if (!string.Equals(SessionId, context.SessionId, StringComparison.Ordinal) ||
            !string.Equals(request.SessionId, context.SessionId, StringComparison.Ordinal))
        {
            Add(
                issues,
                PendingPath + ".sessionId",
                "resource_pending_session_stale",
                request.SessionId,
                context.SessionId);
        }
        if (!string.Equals(
                request.AcceptedRequestId,
                context.AcceptedRequestId,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                PendingPath + ".requests[" + request.RequestId + "].acceptedRequestId",
                "resource_pending_turn_request_stale",
                request.AcceptedRequestId,
                context.AcceptedRequestId);
        }
        if (request.RequestTurn != context.Turn)
        {
            Add(
                issues,
                PendingPath + ".requests[" + request.RequestId + "].requestTurn",
                "resource_pending_turn_stale",
                request.RequestTurn.ToString(CultureInfo.InvariantCulture),
                context.Turn.ToString(CultureInfo.InvariantCulture));
        }
        if (!string.Equals(
                request.FullTurnFingerprint,
                context.FullTurnFingerprint,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                PendingPath + ".requests[" + request.RequestId + "].fullTurnFingerprint",
                "resource_pending_full_turn_stale",
                request.FullTurnFingerprint,
                context.FullTurnFingerprint);
        }
    }

    private static void ValidateCoordinateAndBounds(
        ResourceCoordinate coordinate,
        ResourceOperation operation,
        decimal minimumAmount,
        decimal maximumAmount,
        ResourceDefinitionCatalog definitions,
        string path,
        List<ValidationIssue> issues)
    {
        if (!ResourceMaterializationContract.IsExactIdentifier(coordinate.Realm) ||
            coordinate.Realm is not ("mortal_world" or "chaos_sea" or "shining_abode") ||
            !ResourceMaterializationContract.IsExactIdentifier(coordinate.ResourceOwnerId) ||
            !ResourceMaterializationContract.IsExactIdentifier(coordinate.ResourceKey))
        {
            Add(
                issues,
                path + ".coordinate",
                "resource_pending_coordinate_invalid",
                "exact registered resource coordinate",
                CoordinateText(coordinate));
        }
        if (!definitions.TryResolveExact(coordinate.ResourceKey, out var definition) ||
            definition == null)
        {
            Add(
                issues,
                path + ".coordinate.resourceKey",
                "resource_pending_definition_unknown",
                "one exact sealed resource definition",
                coordinate.ResourceKey);
            return;
        }
        if (!definition.AllowedOwnerKinds.Contains(coordinate.OwnerKind) ||
            !definition.AllowedOperations.Contains(operation))
        {
            Add(
                issues,
                path + ".coordinate",
                "resource_pending_coordinate_operation_forbidden",
                "definition-authorized owner kind and operation",
                CoordinateText(coordinate) + "/" + OperationToken(operation));
        }
        if (minimumAmount < 0m || maximumAmount < minimumAmount ||
            !ResourceMaterializationContract.IsQuantumAligned(
                minimumAmount,
                0m,
                definition.Quantum) ||
            !ResourceMaterializationContract.IsQuantumAligned(
                maximumAmount,
                0m,
                definition.Quantum))
        {
            Add(
                issues,
                path + ".allowedResults",
                "resource_pending_bounds_invalid",
                "non-negative ordered bounds aligned to the sealed resource quantum",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{minimumAmount}..{maximumAmount}"));
        }
    }

    private static void ValidateRequestIdentitySets(
        IReadOnlyList<ResourcePendingRequest> requests,
        IReadOnlyList<ResourcePendingTerminalReceipt> terminals,
        List<ValidationIssue> issues)
    {
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var folded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in requests.Select(static request => request.RequestId)
                     .Concat(terminals.Select(static terminal => terminal.RequestId)))
        {
            if (!exact.Add(id) || !folded.Add(id))
            {
                Add(
                    issues,
                    PendingPath + ".requests",
                    "resource_pending_request_identity_confusable",
                    "exact and ordinal-ignore-case unique request identity",
                    id);
            }
        }
    }

    private static void ValidateCompanions(
        IReadOnlyList<ResourcePendingRequest> requests,
        List<ValidationIssue> issues)
    {
        var ids = requests.Select(static request => request.RequestId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var request in requests)
        {
            foreach (var companion in request.RequiredCompanions)
            {
                if (string.Equals(companion, request.RequestId, StringComparison.Ordinal) ||
                    !ids.Contains(companion))
                {
                    Add(
                        issues,
                        PendingPath + ".requests[" + request.RequestId + "].requiredCompanions",
                        "resource_pending_companion_invalid",
                        "another exact request identity in the same batch",
                        companion);
                }
            }
        }
    }

    private static JsonObject ToCanonicalRequest(ResourcePendingRequest request) =>
        new()
        {
            ["requestId"] = request.RequestId,
            ["sessionId"] = request.SessionId,
            ["acceptedRequestId"] = request.AcceptedRequestId,
            ["requestTurn"] = request.RequestTurn,
            ["eventRef"] = request.EventRef,
            ["effectId"] = request.EffectId,
            ["effectAuthority"] = ToCanonicalAuthorityBinding(
                request.EffectAuthority),
            ["source"] = request.Source,
            ["sourceAuthority"] = ToCanonicalAuthorityBinding(
                request.SourceAuthority),
            ["target"] = request.Target,
            ["targetAuthority"] = ToCanonicalAuthorityBinding(
                request.TargetAuthority),
            ["triggerId"] = request.TriggerId,
            ["coordinate"] = new JsonObject
            {
                ["realm"] = request.Coordinate.Realm,
                ["ownerKind"] = ResourceDefinitionCatalog.GetOwnerKindToken(
                    request.Coordinate.OwnerKind),
                ["resourceOwnerId"] = request.Coordinate.ResourceOwnerId,
                ["resourceKey"] = request.Coordinate.ResourceKey
            },
            ["resourceAuthority"] = ToCanonicalAuthorityBinding(
                request.ResourceAuthority),
            ["operation"] = OperationToken(request.Operation),
            ["allowedResults"] = new JsonArray(
                new JsonObject
                {
                    ["resultKind"] = "narrated_no_state_change"
                },
                new JsonObject
                {
                    ["resultKind"] = "resource_delta",
                    ["operation"] = OperationToken(request.Operation),
                    ["minimumAmount"] = request.MinimumAmount,
                    ["maximumAmount"] = request.MaximumAmount
                }),
            ["sourceAuthorityFingerprint"] = request.SourceAuthorityFingerprint,
            ["policyFingerprint"] = request.PolicyFingerprint,
            ["fullTurnFingerprint"] = request.FullTurnFingerprint,
            ["requiredCompanions"] = new JsonArray(request.RequiredCompanions
                .Select(static value => (JsonNode)value)
                .ToArray()),
            ["fullTurnResubmissionRequired"] = true,
            ["state"] = "pending",
            ["createdAtUtc"] = request.CreatedAtUtc,
            ["replayFingerprint"] = request.ReplayFingerprint,
            ["projection"] = new JsonObject
            {
                ["sourceLabel"] = request.SafeSourceLabel,
                ["targetLabel"] = request.SafeTargetLabel,
                ["resourceLabel"] = request.SafeResourceLabel,
                ["operationLabel"] = request.SafeOperationLabel
            }
        };

    private static JsonObject ToCanonicalAuthorityBinding(
        ResourcePendingAuthorityBinding binding) =>
        new()
        {
            ["bindingKind"] = binding.BindingKind,
            ["authorityId"] = binding.AuthorityId
        };

    private static JsonObject ToCanonicalTerminal(
        ResourcePendingTerminalReceipt terminal)
    {
        var result = new JsonObject
        {
            ["requestId"] = terminal.RequestId,
            ["sessionId"] = terminal.SessionId,
            ["acceptedRequestId"] = terminal.AcceptedRequestId,
            ["requestTurn"] = terminal.RequestTurn,
            ["eventRef"] = terminal.EventRef,
            ["fullTurnFingerprint"] = terminal.FullTurnFingerprint,
            ["requestReplayFingerprint"] = terminal.RequestReplayFingerprint,
            ["resultKind"] = terminal.ResultKind,
            ["reason"] = terminal.Reason,
            ["receiptFingerprint"] = terminal.ReceiptFingerprint,
            ["resolvedAtTurn"] = terminal.ResolvedAtTurn,
            ["state"] = terminal.State
        };
        if (terminal.Amount.HasValue)
            result["amount"] = terminal.Amount.Value;
        return result;
    }

    private static string ComputeRequestReplayFingerprint(
        ResourcePendingRequest request)
    {
        var node = ToCanonicalRequest(request);
        node.Remove("replayFingerprint");
        return Hash("resource-pending-request-v1", node);
    }

    private static string ComputeReceiptFingerprint(
        string requestId,
        string? resultKind,
        decimal? amount,
        string? reason)
    {
        var node = new JsonObject
        {
            ["requestId"] = requestId,
            ["resultKind"] = resultKind,
            ["reason"] = reason
        };
        if (amount.HasValue)
            node["amount"] = amount.Value;
        return Hash("resource-pending-receipt-v1", node);
    }

    private static JsonObject? ParseStrictIdentityObject(
        JsonElement parent,
        string field,
        string path,
        IReadOnlySet<string> allowed,
        IReadOnlyList<string> required,
        List<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty(field, out var node) ||
            node.ValueKind != JsonValueKind.Object)
        {
            Add(
                issues,
                path + "." + field,
                "resource_pending_identity_invalid",
                "strict identity object",
                Describe(parent, field));
            return null;
        }
        ValidateClosedObject(
            node,
            path + "." + field,
            allowed,
            "resource_pending_unknown_field",
            issues);
        foreach (var requiredField in required)
        {
            ReadExact(
                node,
                requiredField,
                path + "." + field,
                issues,
                "resource_pending_identity_invalid");
        }
        return JsonNode.Parse(node.GetRawText())!.AsObject();
    }

    private static ResourcePendingAuthorityBinding? ParseAuthorityBinding(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty(field, out var node) ||
            node.ValueKind != JsonValueKind.Object)
        {
            Add(
                issues,
                path + "." + field,
                "resource_pending_authority_binding_invalid",
                "closed accepted authority binding",
                Describe(parent, field));
            return null;
        }
        ValidateClosedObject(
            node,
            path + "." + field,
            AuthorityBindingFields,
            "resource_pending_unknown_field",
            issues);
        var bindingKind = ReadExact(
            node,
            "bindingKind",
            path + "." + field,
            issues,
            "resource_pending_authority_binding_invalid");
        var authorityId = ReadExact(
            node,
            "authorityId",
            path + "." + field,
            issues,
            "resource_pending_authority_binding_invalid");
        if (bindingKind == null || authorityId == null)
            return null;
        var binding = new ResourcePendingAuthorityBinding(
            bindingKind,
            authorityId);
        ValidateAuthorityBinding(binding, field, issues, path);
        return binding;
    }

    private static void ValidateAuthorityBinding(
        ResourcePendingAuthorityBinding binding,
        string field,
        List<ValidationIssue> issues,
        string path = PendingPath)
    {
        if (!AuthorityBindingKinds.Contains(binding.BindingKind) ||
            !ResourceMaterializationContract.IsExactIdentifier(
                binding.AuthorityId))
        {
            Add(
                issues,
                path + "." + field,
                "resource_pending_authority_binding_invalid",
                "bindingKind permanent|same_turn_ref|accepted_application and one exact authorityId",
                binding.BindingKind + "/" + binding.AuthorityId);
        }
    }

    private static ResourceCoordinate? ParseCoordinate(
        JsonElement parent,
        string path,
        List<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty("coordinate", out var node) ||
            node.ValueKind != JsonValueKind.Object)
        {
            Add(
                issues,
                path + ".coordinate",
                "resource_pending_coordinate_invalid",
                "strict resource coordinate",
                Describe(parent, "coordinate"));
            return null;
        }
        ValidateClosedObject(
            node,
            path + ".coordinate",
            CoordinateFields,
            "resource_pending_unknown_field",
            issues);
        var realm = ReadExact(
            node,
            "realm",
            path + ".coordinate",
            issues,
            "resource_pending_coordinate_invalid");
        var ownerKindToken = ReadExact(
            node,
            "ownerKind",
            path + ".coordinate",
            issues,
            "resource_pending_coordinate_invalid");
        var ownerId = ReadExact(
            node,
            "resourceOwnerId",
            path + ".coordinate",
            issues,
            "resource_pending_coordinate_invalid");
        var resourceKey = ReadExact(
            node,
            "resourceKey",
            path + ".coordinate",
            issues,
            "resource_pending_coordinate_invalid");
        if (ownerKindToken == null ||
            !ResourceDefinitionCatalog.TryParseOwnerKind(ownerKindToken, out var ownerKind))
        {
            Add(
                issues,
                path + ".coordinate.ownerKind",
                "resource_pending_coordinate_invalid",
                "registered resource owner kind",
                ownerKindToken ?? "missing");
            return null;
        }
        return realm == null || ownerId == null || resourceKey == null
            ? null
            : new ResourceCoordinate(realm, ownerKind, ownerId, resourceKey);
    }

    private static ResourceOperation? ParseOperation(
        JsonElement parent,
        string path,
        List<ValidationIssue> issues)
    {
        var token = ReadExact(
            parent,
            "operation",
            path,
            issues,
            "resource_pending_operation_invalid");
        var operation = token switch
        {
            "damage" => ResourceOperation.Damage,
            "restore" => ResourceOperation.Restore,
            "spend" => ResourceOperation.Spend,
            "gain" => ResourceOperation.Gain,
            _ => (ResourceOperation?)null
        };
        if (!operation.HasValue && token != null)
        {
            Add(
                issues,
                path + ".operation",
                "resource_pending_operation_invalid",
                "damage, restore, spend, or gain",
                token);
        }
        return operation;
    }

    private static (decimal? Minimum, decimal? Maximum) ParseAllowedResults(
        JsonElement parent,
        ResourceOperation? operation,
        string path,
        List<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty("allowedResults", out var results) ||
            results.ValueKind != JsonValueKind.Array ||
            results.GetArrayLength() != 2)
        {
            Add(
                issues,
                path + ".allowedResults",
                "resource_pending_allowed_results_invalid",
                "exact narrated_no_state_change and resource_delta result entries",
                Describe(parent, "allowedResults"));
            return (null, null);
        }
        JsonElement? noState = null;
        JsonElement? delta = null;
        foreach (var result in results.EnumerateArray())
        {
            if (result.ValueKind != JsonValueKind.Object)
            {
                Add(
                    issues,
                    path + ".allowedResults",
                    "resource_pending_allowed_results_invalid",
                    "strict allowed-result objects",
                    result.ValueKind.ToString());
                continue;
            }
            ValidateClosedObject(
                result,
                path + ".allowedResults",
                AllowedResultFields,
                "resource_pending_unknown_field",
                issues);
            if (!result.TryGetProperty("resultKind", out var kindNode) ||
                kindNode.ValueKind != JsonValueKind.String)
            {
                continue;
            }
            switch (kindNode.GetString())
            {
                case "narrated_no_state_change":
                    noState = result;
                    break;
                case "resource_delta":
                    delta = result;
                    break;
            }
        }
        if (!noState.HasValue || noState.Value.EnumerateObject().Count() != 1 ||
            !delta.HasValue)
        {
            Add(
                issues,
                path + ".allowedResults",
                "resource_pending_allowed_results_invalid",
                "one closed no-state result and one closed bounded resource-delta result",
                results.GetRawText());
            return (null, null);
        }
        var operationToken = delta.Value.TryGetProperty("operation", out var operationNode) &&
                             operationNode.ValueKind == JsonValueKind.String
            ? operationNode.GetString()
            : null;
        if (operation.HasValue && !string.Equals(
                operationToken,
                OperationToken(operation.Value),
                StringComparison.Ordinal))
        {
            Add(
                issues,
                path + ".allowedResults.operation",
                "resource_pending_operation_mismatch",
                OperationToken(operation.Value),
                operationToken ?? "missing");
        }
        decimal? minimum = delta.Value.TryGetProperty("minimumAmount", out var minimumNode) &&
                           minimumNode.TryGetDecimal(out var parsedMinimum)
            ? parsedMinimum
            : null;
        decimal? maximum = delta.Value.TryGetProperty("maximumAmount", out var maximumNode) &&
                           maximumNode.TryGetDecimal(out var parsedMaximum)
            ? parsedMaximum
            : null;
        if (!minimum.HasValue || !maximum.HasValue)
        {
            Add(
                issues,
                path + ".allowedResults",
                "resource_pending_bounds_invalid",
                "exact decimal minimumAmount and maximumAmount",
                delta.Value.GetRawText());
        }
        return (minimum, maximum);
    }

    private static (string Source, string Target, string Resource, string Operation)?
        ParseProjection(
            JsonElement parent,
            string path,
            List<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty("projection", out var node) ||
            node.ValueKind != JsonValueKind.Object)
        {
            Add(
                issues,
                path + ".projection",
                "resource_pending_projection_invalid",
                "closed safe projection labels",
                Describe(parent, "projection"));
            return null;
        }
        ValidateClosedObject(
            node,
            path + ".projection",
            ProjectionFields,
            "resource_pending_unknown_field",
            issues);
        var source = ReadLabel(node, "sourceLabel", path, issues);
        var target = ReadLabel(node, "targetLabel", path, issues);
        var resource = ReadLabel(node, "resourceLabel", path, issues);
        var operation = ReadLabel(node, "operationLabel", path, issues);
        return source == null || target == null || resource == null || operation == null
            ? null
            : (source, target, resource, operation);
    }

    private static void ValidateSourceTarget(
        JsonObject source,
        JsonObject target,
        string path,
        List<ValidationIssue> issues)
    {
        ValidateJsonObjectFields(source, path + ".source", SourceFields, issues);
        ValidateJsonObjectFields(target, path + ".target", TargetFields, issues);
        foreach (var field in new[] { "kind", "sourceId", "definitionKey" })
        {
            if (!TryReadExact(source[field], out _))
            {
                Add(
                    issues,
                    path + ".source." + field,
                    "resource_pending_identity_invalid",
                    "exact source identity field",
                    Describe(source[field]));
            }
        }
        foreach (var field in new[] { "kind", "targetId" })
        {
            if (!TryReadExact(target[field], out _))
            {
                Add(
                    issues,
                    path + ".target." + field,
                    "resource_pending_identity_invalid",
                    "exact target identity field",
                    Describe(target[field]));
            }
        }
    }

    private static void ValidateJsonObjectFields(
        JsonObject value,
        string path,
        IReadOnlySet<string> fields,
        List<ValidationIssue> issues)
    {
        foreach (var property in value)
        {
            if (!fields.Contains(property.Key))
            {
                Add(
                    issues,
                    path + "." + property.Key,
                    "resource_pending_unknown_field",
                    "registered identity field",
                    property.Key);
            }
        }
    }

    private static void ValidateClosedObject(
        JsonElement value,
        string path,
        IReadOnlySet<string> fields,
        string code,
        List<ValidationIssue> issues)
    {
        foreach (var property in value.EnumerateObject())
        {
            if (!fields.Contains(property.Name))
            {
                Add(
                    issues,
                    path + "." + property.Name,
                    code,
                    "registered current-schema field",
                    property.Name);
            }
        }
    }

    private static void ValidateUniqueProperties(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var exact = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!exact.Add(property.Name))
                {
                    Add(
                        issues,
                        path + "." + property.Name,
                        "resource_pending_duplicate_property",
                        "unique JSON property name",
                        property.Name);
                }
                ValidateUniqueProperties(
                    property.Value,
                    path + "." + property.Name,
                    issues);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                ValidateUniqueProperties(item, $"{path}[{index}]", issues);
                index++;
            }
        }
    }

    private static string? ReadExact(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues,
        string code)
    {
        if (!parent.TryGetProperty(field, out var node) ||
            node.ValueKind != JsonValueKind.String ||
            !ResourceMaterializationContract.IsExactIdentifier(node.GetString()))
        {
            Add(
                issues,
                path + "." + field,
                code,
                "non-empty trimmed exact identifier",
                Describe(parent, field));
            return null;
        }
        return node.GetString();
    }

    private static string? ReadExact(
        JsonObject parent,
        string field,
        string path,
        List<ValidationIssue> issues,
        string code)
    {
        if (!TryReadExact(parent[field], out var value))
        {
            Add(
                issues,
                path + "." + field,
                code,
                "non-empty trimmed exact identifier",
                Describe(parent[field]));
            return null;
        }
        return value;
    }

    private static int? ReadInt(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues,
        string code)
    {
        if (!parent.TryGetProperty(field, out var node) ||
            node.ValueKind != JsonValueKind.Number ||
            !node.TryGetInt32(out var value))
        {
            Add(
                issues,
                path + "." + field,
                code,
                "exact 32-bit integer",
                Describe(parent, field));
            return null;
        }
        return value;
    }

    private static bool ReadBool(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty(field, out var node) ||
            node.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            Add(
                issues,
                path + "." + field,
                "resource_pending_boolean_invalid",
                "boolean",
                Describe(parent, field));
            return false;
        }
        return node.GetBoolean();
    }

    private static string? ReadFingerprint(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty(field, out var node) ||
            node.ValueKind != JsonValueKind.String ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(node.GetString()))
        {
            Add(
                issues,
                path + "." + field,
                "resource_pending_fingerprint_invalid",
                "lowercase SHA-256 authority fingerprint",
                Describe(parent, field));
            return null;
        }
        return node.GetString();
    }

    private static IReadOnlyList<string> ReadExactStringArray(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty(field, out var node) ||
            node.ValueKind != JsonValueKind.Array)
        {
            Add(
                issues,
                path + "." + field,
                "resource_pending_companion_invalid",
                "exact string array",
                Describe(parent, field));
            return Array.Empty<string>();
        }
        var result = new List<string>();
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var folded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in node.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String ||
                !ResourceMaterializationContract.IsExactIdentifier(item.GetString()))
            {
                Add(
                    issues,
                    path + "." + field,
                    "resource_pending_companion_invalid",
                    "exact companion request identity",
                    item.GetRawText());
                continue;
            }
            var value = item.GetString()!;
            if (!exact.Add(value) || !folded.Add(value))
            {
                Add(
                    issues,
                    path + "." + field,
                    "resource_pending_companion_invalid",
                    "exact and ordinal-ignore-case unique companion identities",
                    value);
                continue;
            }
            result.Add(value);
        }
        return result.OrderBy(static value => value, StringComparer.Ordinal).ToArray();
    }

    private static string? ReadUtc(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty(field, out var node) ||
            node.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParseExact(
                node.GetString(),
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed) ||
            parsed.Offset != TimeSpan.Zero)
        {
            Add(
                issues,
                path + "." + field,
                "resource_pending_created_at_invalid",
                "round-trip UTC timestamp",
                Describe(parent, field));
            return null;
        }
        return parsed.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    private static string? ReadReason(
        JsonElement parent,
        string path,
        List<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty("reason", out var node) ||
            node.ValueKind != JsonValueKind.String)
        {
            Add(
                issues,
                path + ".reason",
                "resource_pending_receipt_reason_invalid",
                "non-empty trimmed narrative reason up to 2,000 characters",
                Describe(parent, "reason"));
            return null;
        }
        return ValidateReason(node.GetString(), path, issues);
    }

    private static string? ReadReason(
        JsonObject parent,
        string path,
        List<ValidationIssue> issues)
    {
        if (parent["reason"] is not JsonValue node ||
            !node.TryGetValue<string>(out var text))
        {
            Add(
                issues,
                path + ".reason",
                "resource_pending_receipt_reason_invalid",
                "non-empty trimmed narrative reason up to 2,000 characters",
                Describe(parent["reason"]));
            return null;
        }
        return ValidateReason(text, path, issues);
    }

    private static string? ValidateReason(
        string? value,
        string path,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
            value.Length > 2_000)
        {
            Add(
                issues,
                path + ".reason",
                "resource_pending_receipt_reason_invalid",
                "non-empty trimmed narrative reason up to 2,000 characters",
                Describe(value));
            return null;
        }
        return value;
    }

    private static string? ReadLabel(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty(field, out var node) ||
            node.ValueKind != JsonValueKind.String)
        {
            Add(
                issues,
                path + ".projection." + field,
                "resource_pending_projection_invalid",
                "non-empty trimmed safe label",
                Describe(parent, field));
            return null;
        }
        var value = node.GetString();
        if (!IsSafeLabel(value))
        {
            Add(
                issues,
                path + ".projection." + field,
                "resource_pending_projection_invalid",
                "non-empty trimmed safe label up to 200 characters",
                Describe(value));
            return null;
        }
        return value;
    }

    private static void ValidateSafeLabel(
        string value,
        string field,
        List<ValidationIssue> issues)
    {
        if (!IsSafeLabel(value))
        {
            Add(
                issues,
                PendingPath + ".projection." + field,
                "resource_pending_projection_invalid",
                "non-empty trimmed safe label up to 200 characters",
                Describe(value));
        }
    }

    private static bool IsSafeLabel(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
        value.Length <= 200 &&
        !value.Contains("game_state/", StringComparison.OrdinalIgnoreCase) &&
        !value.Contains("sha256:", StringComparison.OrdinalIgnoreCase);

    private static void ValidateExact(
        string value,
        string field,
        string code,
        List<ValidationIssue> issues)
    {
        if (!ResourceMaterializationContract.IsExactIdentifier(value))
        {
            Add(
                issues,
                PendingPath + "." + field,
                code,
                "non-empty trimmed exact identifier",
                Describe(value));
        }
    }

    private static void ValidateFingerprint(
        string value,
        string field,
        List<ValidationIssue> issues)
    {
        if (!ResourceMaterializationContract.IsAuthorityFingerprint(value))
        {
            Add(
                issues,
                PendingPath + "." + field,
                "resource_pending_fingerprint_invalid",
                "lowercase SHA-256 authority fingerprint",
                Describe(value));
        }
    }

    private static bool TryReadExact(JsonNode? node, out string value)
    {
        value = node is JsonValue jsonValue &&
                jsonValue.TryGetValue<string>(out var text) &&
                ResourceMaterializationContract.IsExactIdentifier(text)
            ? text
            : string.Empty;
        return value.Length != 0;
    }

    private static bool TryReadDecimal(JsonNode? node, out decimal value)
    {
        value = 0m;
        if (node == null)
            return false;
        try
        {
            using var document = JsonDocument.Parse(node.ToJsonString());
            return document.RootElement.ValueKind == JsonValueKind.Number &&
                   document.RootElement.TryGetDecimal(out value);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string OperationToken(ResourceOperation operation) => operation switch
    {
        ResourceOperation.Damage => "damage",
        ResourceOperation.Restore => "restore",
        ResourceOperation.Spend => "spend",
        ResourceOperation.Gain => "gain",
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
    };

    private static string CoordinateText(ResourceCoordinate coordinate) =>
        $"{coordinate.Realm}/{ResourceDefinitionCatalog.GetOwnerKindToken(coordinate.OwnerKind)}/" +
        $"{coordinate.ResourceOwnerId}/{coordinate.ResourceKey}";

    private static string Hash(string domain, JsonNode node)
    {
        var bytes = Encoding.UTF8.GetBytes(domain + "\0" + node.ToJsonString());
        return "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static HashSet<string> Set(params string[] values) =>
        new(values, StringComparer.Ordinal);

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        ResourceMaterializationContract.AddIssue(
            issues,
            path,
            code,
            expected,
            actual,
            code.Contains("client", StringComparison.Ordinal) ||
            code.Contains("receipt", StringComparison.Ordinal)
                ? IssueCategory.ClientOwnedSurface
                : IssueCategory.StateConsistency);

    private static ResourcePendingResolutionCreationResult FailedCreation(
        IReadOnlyList<ValidationIssue> issues) =>
        new(null, null, issues.ToArray());

    private static string Describe(JsonNode? node) => node?.ToJsonString() ?? "missing";

    private static string Describe(string? value) => value ?? "missing";

    private static string Describe(JsonElement parent, string field) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(field, out var node)
            ? node.GetRawText()
            : "missing";

    private sealed record ParsedReceipt(
        string Path,
        string? RequestId,
        string? ResultKind,
        decimal? Amount,
        string? Reason);
}
