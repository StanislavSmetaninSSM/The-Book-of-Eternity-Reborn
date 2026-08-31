using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.Services;

internal sealed class MortalWoundTreatmentPersistedRequestCatalogResult
{
    private readonly ReadOnlyCollection<ValidationIssue> _issues;
    private readonly ReadOnlyCollection<MortalWoundTreatmentAttemptRequest> _requests;

    internal MortalWoundTreatmentPersistedRequestCatalogResult(
        bool isValid,
        IEnumerable<ValidationIssue> issues,
        IEnumerable<MortalWoundTreatmentAttemptRequest> requests)
    {
        IsValid = isValid;
        _issues = MortalWoundTreatmentShellDetachment.Freeze(issues);
        _requests = MortalWoundTreatmentShellDetachment.Freeze(requests);
    }

    public bool IsValid { get; }
    public IReadOnlyList<ValidationIssue> Issues => _issues;
    public IReadOnlyList<MortalWoundTreatmentAttemptRequest> Requests => _requests;
}

internal sealed class MortalWoundTreatmentRequirementEvidenceJsonConverter :
    JsonConverter<MortalWoundTreatmentRequirementEvidence>
{
    public override MortalWoundTreatmentRequirementEvidence? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("kind", out var kindElement) ||
            kindElement.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("Requirement evidence needs one exact kind.");
        }

        var kind = kindElement.GetString();
        var raw = root.GetRawText();
        MortalWoundTreatmentRequirementEvidence? value = kind switch
        {
            "item_quantity" => JsonSerializer.Deserialize<
                MortalWoundItemQuantityRequirementEvidence>(raw, options),
            "resource_quantity" => JsonSerializer.Deserialize<
                MortalWoundResourceQuantityRequirementEvidence>(raw, options),
            "skill_tier" => JsonSerializer.Deserialize<
                MortalWoundSkillTierRequirementEvidence>(raw, options),
            "source_capability" => JsonSerializer.Deserialize<
                MortalWoundSourceCapabilityRequirementEvidence>(raw, options),
            "provider" => JsonSerializer.Deserialize<
                MortalWoundProviderRequirementEvidence>(raw, options),
            "consent" => JsonSerializer.Deserialize<
                MortalWoundConsentRequirementEvidence>(raw, options),
            "facility" => JsonSerializer.Deserialize<
                MortalWoundFacilityRequirementEvidence>(raw, options),
            "location" => JsonSerializer.Deserialize<
                MortalWoundLocationRequirementEvidence>(raw, options),
            "quest_state" => JsonSerializer.Deserialize<
                MortalWoundQuestStateRequirementEvidence>(raw, options),
            "effect_state" => JsonSerializer.Deserialize<
                MortalWoundEffectStateRequirementEvidence>(raw, options),
            "environment" => JsonSerializer.Deserialize<
                MortalWoundEnvironmentRequirementEvidence>(raw, options),
            _ => throw new JsonException("Unknown requirement-evidence kind.")
        };
        if (value is null || !string.Equals(value.Kind, kind, StringComparison.Ordinal))
            throw new JsonException("Requirement-evidence kind mismatch.");
        return value;
    }

    public override void Write(
        Utf8JsonWriter writer,
        MortalWoundTreatmentRequirementEvidence value,
        JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
}

internal static class MortalWoundTreatmentPersistedRequestCatalog
{
    private sealed record PersistedCandidate(
        JsonObject Serialized,
        MortalWoundTreatmentAttemptRequest Request,
        string Origin);

    internal static MortalWoundTreatmentPersistedRequestCatalogResult Parse(
        JsonElement? commandRoot,
        JsonElement? pendingRoot,
        WoundHistoryParseResult history)
    {
        ArgumentNullException.ThrowIfNull(history);
        if (!history.IsValid)
        {
            return new MortalWoundTreatmentPersistedRequestCatalogResult(
                false,
                history.Issues,
                Array.Empty<MortalWoundTreatmentAttemptRequest>());
        }

        var issues = new List<ValidationIssue>();
        var candidates = new List<PersistedCandidate>();
        foreach (var transition in history.State!.Transitions)
        {
            if (transition.TreatmentResult is not { } treatmentResult)
                continue;
            candidates.Add(new PersistedCandidate(
                treatmentResult.SerializeRequest(),
                treatmentResult.Request,
                "history"));
        }
        (string SessionId, string RequestId, string SnapshotToken)? commandBinding = null;
        (string SessionId, string RequestId, string SnapshotToken)? pendingBinding = null;
        var pendingHasRepairWave = false;

        if (commandRoot is { } commandElement)
        {
            var parsed = WoundResponseInputComposer.ParseCommandRoot(commandElement);
            if (!parsed.Success)
            {
                issues.AddRange(parsed.Issues);
            }
            else if (parsed.TreatmentCommands.Count != 0 &&
                     parsed.Commands.Count != 0)
            {
                issues.Add(PersistenceIssue(
                    "mortal_wound_treatment_persisted_mixed_command_families",
                    "one homogeneous wound command family",
                    "opportunity and treatment commands are mixed"));
            }
            else
            {
                commandBinding = ReadBinding(commandElement, "command", issues);
                foreach (var draft in parsed.TreatmentCommands)
                {
                    if (MortalWoundTreatmentCommandCodec.TryParseRequest(
                            draft.Request,
                            AcceptedMechanicsPlan.WoundCommandPath + ".authority.request",
                            issues,
                            out var request) &&
                        request is not null)
                    {
                        candidates.Add(new PersistedCandidate(
                            draft.Request.DeepClone().AsObject(),
                            request,
                            "command"));
                    }
                }
            }
        }

        if (pendingRoot is { } pendingElement)
        {
            pendingBinding = ParsePending(
                pendingElement,
                candidates,
                issues,
                out pendingHasRepairWave);
        }

        if (commandBinding.HasValue && pendingBinding.HasValue &&
            commandBinding.Value != pendingBinding.Value)
        {
            issues.Add(PersistenceIssue(
                "mortal_wound_treatment_persisted_binding_mismatch",
                "the exact same session/request/snapshot binding",
                "command and pending roots disagree"));
        }
        if (commandRoot.HasValue && pendingRoot.HasValue &&
            pendingHasRepairWave &&
            !HasExactCrossRootTreatmentAgreement(candidates))
        {
            issues.Add(PersistenceIssue(
                "mortal_wound_treatment_persisted_cross_root_mismatch",
                "the exact command treatment set copied into its real repair wave",
                "command and pending treatment sets disagree"));
        }

        var unique = Coalesce(candidates, issues);
        if (issues.Count != 0)
        {
            return new MortalWoundTreatmentPersistedRequestCatalogResult(
                false,
                issues,
                Array.Empty<MortalWoundTreatmentAttemptRequest>());
        }
        return new MortalWoundTreatmentPersistedRequestCatalogResult(
            true,
            Array.Empty<ValidationIssue>(),
            unique.Select(static candidate => candidate.Request));
    }

    private static (string SessionId, string RequestId, string SnapshotToken)? ParsePending(
        JsonElement root,
        ICollection<PersistedCandidate> candidates,
        ICollection<ValidationIssue> issues,
        out bool hasRepairWave)
    {
        hasRepairWave = false;
        const string path = WoundAcceptedTurnSnapshotContract.PendingResolutionPath;
        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(PersistenceIssue(
                "mortal_wound_treatment_persisted_pending_invalid",
                "one strict pending root",
                root.ValueKind.ToString()));
            return null;
        }

        var allowed = new HashSet<string>(new[]
        {
            "schemaVersion", "sessionId", "requestId", "snapshotToken",
            "repairPackets", "repairReceipts", "submittedTreatmentRequests"
        }, StringComparer.Ordinal);
        var required = allowed.Where(static name =>
            name != "submittedTreatmentRequests").ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!seen.Add(property.Name) || !allowed.Contains(property.Name))
            {
                issues.Add(PersistenceIssue(
                    "mortal_wound_treatment_persisted_pending_invalid",
                    "one closed pending root with unique fields",
                    property.Name));
            }
        }
        if (!required.IsSubsetOf(seen) ||
            !root.TryGetProperty("schemaVersion", out var schema) ||
            schema.ValueKind != JsonValueKind.Number ||
            !schema.TryGetInt32(out var schemaVersion) || schemaVersion != 1 ||
            !root.TryGetProperty("repairPackets", out var packets) ||
            packets.ValueKind != JsonValueKind.Array ||
            !root.TryGetProperty("repairReceipts", out var receipts) ||
            receipts.ValueKind != JsonValueKind.Array)
        {
            issues.Add(PersistenceIssue(
                "mortal_wound_treatment_persisted_pending_invalid",
                "one complete version-1 pending root",
                path));
            return null;
        }

        var binding = ReadBinding(root, "pending", issues);
        var hasSubmitted = root.TryGetProperty(
            "submittedTreatmentRequests",
            out var submitted);
        hasRepairWave = packets.GetArrayLength() != 0 ||
                        receipts.GetArrayLength() != 0;
        if ((hasSubmitted || hasRepairWave) &&
            (!binding.HasValue ||
             !WoundRepairPacketBuilder.IsValidPersistedRepairWave(
                 packets,
                 receipts,
                 binding.Value.SessionId,
                 binding.Value.RequestId,
                 binding.Value.SnapshotToken)))
        {
            issues.Add(PersistenceIssue(
                "mortal_wound_treatment_persisted_pending_invalid",
                "one strict bound repair-packet and receipt wave",
                path));
            return binding;
        }
        if (!hasSubmitted)
            return binding;
        if (submitted.ValueKind != JsonValueKind.Array ||
            submitted.GetArrayLength() is < 1 or
                > WoundResponseInputComposer.MaximumAcceptedCommandCount)
        {
            issues.Add(PersistenceIssue(
                "mortal_wound_treatment_persisted_pending_invalid",
                $"one to {WoundResponseInputComposer.MaximumAcceptedCommandCount} submitted treatment requests",
                submitted.ValueKind == JsonValueKind.Array
                    ? submitted.GetArrayLength().ToString(CultureInfo.InvariantCulture)
                    : submitted.ValueKind.ToString()));
            return binding;
        }

        var index = 0;
        foreach (var rowElement in submitted.EnumerateArray())
        {
            var rowPath = path + $".submittedTreatmentRequests[{index++}]";
            if (rowElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(PersistenceIssue(
                    "mortal_wound_treatment_persisted_pending_invalid",
                    "one closed submitted-request row",
                    rowElement.ValueKind.ToString()));
                continue;
            }
            var rowNames = rowElement.EnumerateObject()
                .Select(static property => property.Name)
                .ToArray();
            var expected = new[]
            {
                "operationKey", "attemptId", "requestFingerprint", "request"
            };
            if (rowNames.Length != expected.Length ||
                rowNames.Distinct(StringComparer.Ordinal).Count() != expected.Length ||
                !rowNames.ToHashSet(StringComparer.Ordinal)
                    .SetEquals(expected))
            {
                issues.Add(PersistenceIssue(
                    "mortal_wound_treatment_persisted_pending_invalid",
                    "one closed submitted-request row",
                    rowPath));
                continue;
            }
            var operationKey = ReadString(rowElement, "operationKey");
            var attemptId = ReadString(rowElement, "attemptId");
            var requestFingerprint = ReadString(rowElement, "requestFingerprint");
            if (!ResourceMaterializationContract.IsExactIdentifier(operationKey) ||
                !ResourceMaterializationContract.IsExactIdentifier(attemptId) ||
                !ResourceMaterializationContract.IsAuthorityFingerprint(requestFingerprint) ||
                !rowElement.TryGetProperty("request", out var requestElement) ||
                requestElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(PersistenceIssue(
                    "mortal_wound_treatment_persisted_pending_invalid",
                    "one complete submitted-request coordinate and graph",
                    rowPath));
                continue;
            }
            if (!MortalWoundTreatmentCommandCodec.HasNoDuplicateProperties(
                    requestElement,
                    rowPath + ".request",
                    issues))
            {
                continue;
            }
            var serialized = JsonNode.Parse(requestElement.GetRawText())!.AsObject();
            if (!MortalWoundTreatmentCommandCodec.TryParseRequest(
                    serialized,
                    rowPath + ".request",
                    issues,
                    out var request) || request is null)
            {
                continue;
            }
            if (!binding.HasValue ||
                !MortalWoundTreatmentCommandCodec.MatchesRootBinding(
                    request.Coordinates,
                    binding.Value.SessionId,
                    binding.Value.RequestId,
                    binding.Value.SnapshotToken))
            {
                issues.Add(PersistenceIssue(
                    "mortal_wound_treatment_persisted_root_binding_mismatch",
                    "pending root binding matching every sealed request coordinate",
                    rowPath));
                continue;
            }
            if (!string.Equals(operationKey, request.Coordinates.OperationKey,
                    StringComparison.Ordinal) ||
                !string.Equals(attemptId, request.Coordinates.AttemptId,
                    StringComparison.Ordinal) ||
                !string.Equals(requestFingerprint, request.RequestFingerprint,
                    StringComparison.Ordinal))
            {
                issues.Add(PersistenceIssue(
                    "mortal_wound_treatment_persisted_pending_coordinate_mismatch",
                    "outer pending coordinates matching the sealed request",
                    rowPath));
                continue;
            }
            candidates.Add(new PersistedCandidate(serialized, request, "pending"));
        }
        return binding;
    }

    private static IReadOnlyList<PersistedCandidate> Coalesce(
        IEnumerable<PersistedCandidate> candidates,
        ICollection<ValidationIssue> issues)
    {
        var unique = new List<PersistedCandidate>();
        var observed = new List<PersistedCandidate>();
        foreach (var candidate in candidates)
        {
            var sameOriginDuplicate = observed.FirstOrDefault(existing =>
                string.Equals(existing.Origin, candidate.Origin, StringComparison.Ordinal) &&
                HasExactLogicalCoordinate(existing, candidate));
            observed.Add(candidate);
            if (sameOriginDuplicate is not null)
            {
                issues.Add(PersistenceIssue(
                    "mortal_wound_treatment_persisted_origin_duplicate",
                    "one exact treatment request coordinate per persisted origin",
                    candidate.Origin));
                continue;
            }
            var exact = unique.FirstOrDefault(existing =>
                HasExactLogicalCoordinate(existing, candidate));
            if (exact is not null)
            {
                if (!MortalWoundTreatmentCommandCodec.SemanticJsonEquals(
                        exact.Serialized,
                        candidate.Serialized))
                {
                    issues.Add(PersistenceIssue(
                        "mortal_wound_treatment_persisted_request_copy_mismatch",
                        "semantically identical duplicate authority graphs",
                        exact.Origin + "/" + candidate.Origin));
                }
                continue;
            }

            var course = candidate.Request.ModeAuthority as MortalWoundCourseModeAuthority;
            var collision = unique.FirstOrDefault(existing =>
                ConfusableIdentifierEquals(
                    existing.Request.Coordinates.OperationKey,
                    candidate.Request.Coordinates.OperationKey) ||
                ConfusableIdentifierEquals(
                    existing.Request.Coordinates.AttemptId,
                    candidate.Request.Coordinates.AttemptId) ||
                ConfusableIdentifierEquals(
                    existing.Request.Coordinates.EventRef,
                    candidate.Request.Coordinates.EventRef) ||
                string.Equals(existing.Request.RequestFingerprint,
                    candidate.Request.RequestFingerprint, StringComparison.Ordinal) ||
                (course is not null &&
                 existing.Request.ModeAuthority is MortalWoundCourseModeAuthority existingCourse &&
                 ConfusableIdentifierEquals(existingCourse.CourseId, course.CourseId) &&
                 existingCourse.MilestoneOrdinal == course.MilestoneOrdinal));
            if (collision is not null)
            {
                issues.Add(PersistenceIssue(
                    "mortal_wound_treatment_persisted_coordinate_collision",
                    "one unique operation, attempt, accepted event, fingerprint, and course milestone",
                    collision.Origin + "/" + candidate.Origin));
                continue;
            }
            unique.Add(candidate);
        }
        return unique;
    }

    private static bool HasExactCrossRootTreatmentAgreement(
        IReadOnlyCollection<PersistedCandidate> candidates)
    {
        var command = candidates.Where(static candidate =>
            string.Equals(candidate.Origin, "command", StringComparison.Ordinal)).ToArray();
        var pending = candidates.Where(static candidate =>
            string.Equals(candidate.Origin, "pending", StringComparison.Ordinal)).ToArray();
        if (command.Length != pending.Length)
            return false;
        foreach (var candidate in command)
        {
            var matches = pending.Where(other =>
                HasExactLogicalCoordinate(candidate, other)).ToArray();
            if (matches.Length != 1 ||
                !MortalWoundTreatmentCommandCodec.SemanticJsonEquals(
                    candidate.Serialized,
                    matches[0].Serialized))
            {
                return false;
            }
        }
        return true;
    }

    private static bool HasExactLogicalCoordinate(
        PersistedCandidate left,
        PersistedCandidate right) =>
        string.Equals(
            left.Request.Coordinates.OperationKey,
            right.Request.Coordinates.OperationKey,
            StringComparison.Ordinal) &&
        string.Equals(
            left.Request.Coordinates.AttemptId,
            right.Request.Coordinates.AttemptId,
            StringComparison.Ordinal) &&
        string.Equals(
            left.Request.RequestFingerprint,
            right.Request.RequestFingerprint,
            StringComparison.Ordinal);

    private static bool ConfusableIdentifierEquals(string left, string right) =>
        string.Equals(
            ExactIdentifierConfusableKey.Build(left),
            ExactIdentifierConfusableKey.Build(right),
            StringComparison.Ordinal);

    private static (string SessionId, string RequestId, string SnapshotToken)? ReadBinding(
        JsonElement root,
        string origin,
        ICollection<ValidationIssue> issues)
    {
        var sessionId = ReadString(root, "sessionId");
        var requestId = ReadString(root, "requestId");
        var snapshotToken = ReadString(root, "snapshotToken");
        if (!ResourceMaterializationContract.IsExactIdentifier(sessionId) ||
            !ResourceMaterializationContract.IsExactIdentifier(requestId) ||
            !ResourceMaterializationContract.IsExactIdentifier(snapshotToken))
        {
            issues.Add(PersistenceIssue(
                "mortal_wound_treatment_persisted_binding_invalid",
                "one complete exact root binding",
                origin));
            return null;
        }
        return (sessionId!, requestId!, snapshotToken!);
    }

    private static string? ReadString(JsonElement source, string property) =>
        source.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static ValidationIssue PersistenceIssue(
        string code,
        string expected,
        string actual) => new(
        AcceptedMechanicsPlan.WoundCommandPath,
        IssueSeverity.Error,
        "The persisted Mortal wound-treatment authority cannot be trusted.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual);
}

internal static class MortalWoundTreatmentCommandCodec
{
    private static readonly JsonSerializerOptions OperationJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private static readonly JsonSerializerOptions RequestJsonOptions = CreateRequestJsonOptions();

    private static readonly IReadOnlySet<string> CommandFields = new HashSet<string>(
        new[]
        {
            "kind", "transitionKind", "commandRef", "operationKey", "authority",
            "result", "finalSceneText"
        },
        StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> ResultFields = new HashSet<string>(
        new[]
        {
            "kind", "mode", "routeId", "attemptDisposition", "resultCategory",
            "selectedOutcomeIndex", "interruption", "declaredResult",
            "consumptionTrigger", "requestAuthority", "modeEvidence",
            "routeFingerprint", "resolutionAuthorityFingerprint", "resultFingerprint",
            "routeCompletion", "receiptFingerprint"
        },
        StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> RequestFields = new HashSet<string>(
        new[]
        {
            "mode", "coordinates", "milestoneOrdinal", "routeSourceWound",
            "routeSourceWoundFingerprint", "modeAuthority",
            "requirementAuthority", "resourceAuthority", "requestFingerprint"
        },
        StringComparer.Ordinal);

    internal static bool IsTreatmentCommand(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty("kind", out var kind) &&
        kind.ValueKind == JsonValueKind.String &&
        string.Equals(kind.GetString(), "accepted_transition", StringComparison.Ordinal) &&
        element.TryGetProperty("transitionKind", out var transitionKind) &&
        transitionKind.ValueKind == JsonValueKind.String &&
        string.Equals(transitionKind.GetString(), "treat", StringComparison.Ordinal);

    internal static MortalWoundTreatmentCommandDraft? Parse(
        JsonElement element,
        int index,
        string? rootSessionId,
        string? rootRequestId,
        string? rootSnapshotToken,
        ICollection<ValidationIssue> issues)
    {
        var path = $"{AcceptedMechanicsPlan.WoundCommandPath}.commands[{index}]";
        var start = issues.Count;
        if (!HasNoDuplicateProperties(element, path, issues))
            return null;
        if (!TryClosedObject(element, CommandFields, path, issues, out var command))
            return null;
        if (!ExactString(command, "kind", "accepted_transition") ||
            !ExactString(command, "transitionKind", "treat"))
        {
            Add(issues, path, "wound_command_invalid_treatment_kind",
                "accepted_transition/treat", "another command kind");
        }
        var commandRef = ReadIdentifier(command, "commandRef", path, issues);
        var outerOperationKey = ReadIdentifier(command, "operationKey", path, issues);
        var finalSceneText = ReadReadable(command, "finalSceneText", path, issues);
        if (!TrySingleObject(command, "authority", path, issues, out var authority) ||
            !TryClosedObject(
                authority,
                new HashSet<string>(new[] { "request" }, StringComparer.Ordinal),
                path + ".authority",
                issues,
                out authority) ||
            !TrySingleObject(authority, "request", path + ".authority", issues, out var request))
        {
            return null;
        }
        if (!TrySingleObject(command, "result", path, issues, out var result) ||
            !TryClosedObject(result, ResultFields, path + ".result", issues, out result))
        {
            return null;
        }
        if (!TrySingleObject(result, "requestAuthority", path + ".result", issues,
                out var resultRequest) ||
            !SemanticJsonEquals(request, resultRequest))
        {
            Add(issues, path + ".result.requestAuthority",
                "mortal_wound_treatment_persisted_request_copy_mismatch",
                "the exact complete authority.request graph",
                "result request copy differs");
            return null;
        }
        if (!TryClosedObject(request, RequestFields, path + ".authority.request", issues,
                out request))
        {
            return null;
        }

        var mode = ReadClosedString(request, "mode", path + ".authority.request", issues,
            "procedure", "course", "guaranteed");
        if (!TrySingleObject(request, "coordinates", path + ".authority.request", issues,
                out var coordinates))
        {
            return null;
        }
        var operationKey = ReadIdentifier(
            coordinates,
            "operationKey",
            path + ".authority.request.coordinates",
            issues);
        var attemptId = ReadIdentifier(
            coordinates,
            "attemptId",
            path + ".authority.request.coordinates",
            issues);
        var routeId = ReadIdentifier(
            coordinates,
            "routeId",
            path + ".authority.request.coordinates",
            issues);
        var coordinatesFingerprint = ReadFingerprint(
            coordinates,
            "coordinatesFingerprint",
            path + ".authority.request.coordinates",
            issues);
        var requestFingerprint = ReadFingerprint(
            request,
            "requestFingerprint",
            path + ".authority.request",
            issues);
        if (!string.Equals(operationKey, outerOperationKey, StringComparison.Ordinal))
        {
            Add(issues, path + ".operationKey",
                "mortal_wound_treatment_persisted_outer_coordinate_mismatch",
                operationKey ?? "one request operation key",
                outerOperationKey ?? "missing");
        }
        if (!ExactString(result, "kind", "treat") ||
            !ExactString(result, "mode", mode) ||
            !ExactString(result, "routeId", routeId))
        {
            Add(issues, path + ".result",
                "mortal_wound_treatment_persisted_result_coordinate_mismatch",
                "treat plus the exact request mode and route",
                "outer result disagrees");
        }

        var attemptDispositionToken = ReadClosedString(
            result,
            "attemptDisposition",
            path + ".result",
            issues,
            "accepted_terminal");
        var resultCategory = ReadClosedString(
            result,
            "resultCategory",
            path + ".result",
            issues,
            "success", "partial_success", "failed_attempt");
        var selectedOutcomeIndex = ReadNullableNonNegativeInt32(
            result,
            "selectedOutcomeIndex",
            path + ".result",
            issues);
        var interruption = ReadBoolean(result, "interruption", path + ".result", issues);
        var consumptionTrigger = ReadClosedString(
            result,
            "consumptionTrigger",
            path + ".result",
            issues,
            "success", "partial_success", "failed_attempt", "none");
        var routeCompletionToken = ReadClosedString(
            result,
            "routeCompletion",
            path + ".result",
            issues,
            "append_once", "none");
        var routeFingerprint = ReadFingerprint(
            result,
            "routeFingerprint",
            path + ".result",
            issues);
        var carriedResolutionFingerprint = ReadFingerprint(
            result,
            "resolutionAuthorityFingerprint",
            path + ".result",
            issues);
        var carriedResultFingerprint = ReadFingerprint(
            result,
            "resultFingerprint",
            path + ".result",
            issues);
        var carriedReceiptFingerprint = ReadFingerprint(
            result,
            "receiptFingerprint",
            path + ".result",
            issues);

        var declaredResult = ParseOperations(
            result,
            "declaredResult",
            path + ".result",
            issues);
        if (!TrySingleObject(result, "modeEvidence", path + ".result", issues,
                out var modeEvidence))
        {
            return null;
        }
        string? modeEvidenceFingerprint = null;
        string? reactionFingerprint = null;

        if (!TrySingleObject(request, "requirementAuthority", path + ".authority.request",
                issues, out var requirementAuthority) ||
            !TrySingleObject(request, "resourceAuthority", path + ".authority.request",
                issues, out var resourceAuthority))
        {
            return null;
        }
        var requirementFingerprint = ReadFingerprint(
            requirementAuthority,
            "authorityFingerprint",
            path + ".authority.request.requirementAuthority",
            issues);
        var resourceFingerprint = ReadFingerprint(
            resourceAuthority,
            "authorityFingerprint",
            path + ".authority.request.resourceAuthority",
            issues);

        string? courseId = null;
        int? courseMilestoneOrdinal = null;
        string? courseDisposition = null;
        if (mode == "course")
        {
            if (!TrySingleObject(request, "modeAuthority", path + ".authority.request",
                    issues, out var courseAuthority))
            {
                return null;
            }
            courseId = ReadIdentifier(
                courseAuthority,
                "courseId",
                path + ".authority.request.modeAuthority",
                issues);
            courseMilestoneOrdinal = ReadPositiveInt32(
                courseAuthority,
                "milestoneOrdinal",
                path + ".authority.request.modeAuthority",
                issues);
        }

        if (issues.Count != start ||
            mode is null || operationKey is null || attemptId is null || routeId is null ||
            coordinatesFingerprint is null || requestFingerprint is null ||
            resultCategory is null || consumptionTrigger is null ||
            routeCompletionToken is null || routeFingerprint is null ||
            carriedResolutionFingerprint is null || carriedResultFingerprint is null ||
            carriedReceiptFingerprint is null || commandRef is null ||
            requirementFingerprint is null || resourceFingerprint is null ||
            declaredResult is null || attemptDispositionToken is null)
        {
            return null;
        }

        if (!TryParseRequest(
                request,
                path + ".authority.request",
                issues,
                out var parsedRequest) ||
            parsedRequest is null)
        {
            return null;
        }
        if (!MatchesRootBinding(
                parsedRequest.Coordinates,
                rootSessionId,
                rootRequestId,
                rootSnapshotToken))
        {
            Add(issues, AcceptedMechanicsPlan.WoundCommandPath,
                "mortal_wound_treatment_persisted_root_binding_mismatch",
                "root session/request/snapshot matching the sealed request coordinates",
                "command root and request coordinates disagree");
            return null;
        }
        if (!TryValidateModeEvidence(
                modeEvidence,
                parsedRequest,
                selectedOutcomeIndex,
                path + ".result.modeEvidence",
                issues,
                out modeEvidenceFingerprint,
                out reactionFingerprint,
                out courseDisposition) ||
            modeEvidenceFingerprint is null)
        {
            return null;
        }

        var routeCompletion = routeCompletionToken == "append_once" ? "AppendOnce" : "None";
        if (!HasMatchingResultSemantics(
                parsedRequest,
                resultCategory,
                selectedOutcomeIndex,
                interruption,
                consumptionTrigger,
                courseDisposition,
                routeFingerprint,
                routeCompletion) ||
            !HasMatchingDeclaredResult(parsedRequest, declaredResult))
        {
            Add(issues, path + ".result",
                "mortal_wound_treatment_persisted_result_semantics_mismatch",
                "actionable result fields derived from the complete sealed request",
                "one or more result fields disagree");
            return null;
        }
        var expectedResolutionFingerprint =
            MortalWoundTreatmentResolution.ComputeResolutionAuthorityFingerprint(
                requestFingerprint,
                mode,
                "AcceptedTerminal",
                resultCategory,
                selectedOutcomeIndex,
                interruption,
                consumptionTrigger,
                courseId,
                courseMilestoneOrdinal,
                courseDisposition,
                routeFingerprint,
                routeCompletion,
                reactionFingerprint,
                modeEvidenceFingerprint);
        var expectedResultFingerprint = MortalWoundTreatmentResolution.ComputeResultFingerprint(
            expectedResolutionFingerprint,
            declaredResult);
        var expectedReceiptFingerprint = MortalWoundTreatmentReceipt.ComputePersistedFingerprint(
            mode,
            coordinatesFingerprint,
            "AcceptedTerminal",
            resultCategory,
            selectedOutcomeIndex,
            interruption,
            declaredResult,
            consumptionTrigger,
            courseId,
            courseMilestoneOrdinal,
            courseDisposition,
            requirementFingerprint,
            resourceFingerprint,
            modeEvidenceFingerprint,
            routeFingerprint,
            expectedResolutionFingerprint,
            requestFingerprint,
            expectedResultFingerprint,
            routeCompletion);
        if (!string.Equals(
                carriedResolutionFingerprint,
                expectedResolutionFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                carriedResultFingerprint,
                expectedResultFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                carriedReceiptFingerprint,
                expectedReceiptFingerprint,
                StringComparison.Ordinal))
        {
            Add(issues, path + ".result",
                "mortal_wound_treatment_persisted_result_seal_mismatch",
                "recomputable resolution, result, and receipt fingerprints",
                "one or more carried seals disagree");
            return null;
        }

        var expectedCommandRef = ComputeCommandRef(
            rootSessionId!,
            rootRequestId!,
            rootSnapshotToken!,
            operationKey,
            requestFingerprint,
            carriedResultFingerprint,
            finalSceneText!);
        if (!string.Equals(commandRef, expectedCommandRef, StringComparison.Ordinal))
        {
            Add(issues, path + ".commandRef",
                "mortal_wound_treatment_persisted_command_ref_mismatch",
                expectedCommandRef,
                commandRef);
            return null;
        }

        return new MortalWoundTreatmentCommandDraft(
            command.DeepClone().AsObject(),
            request.DeepClone().AsObject(),
            rootSessionId!,
            rootRequestId!,
            rootSnapshotToken!,
            operationKey,
            attemptId,
            parsedRequest.Coordinates.EventRef,
            requestFingerprint,
            courseId,
            courseMilestoneOrdinal,
            finalSceneText!);
    }

    internal static bool TryParseHistoryResult(
        JsonElement element,
        string path,
        ICollection<ValidationIssue> issues,
        out MortalWoundTreatmentAttemptRequest? request,
        out MortalWoundTreatmentReceipt? receipt,
        out JsonObject? canonicalResult)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(issues);
        request = null;
        receipt = null;
        canonicalResult = null;
        var start = issues.Count;
        if (!HasNoDuplicateProperties(element, path, issues) ||
            element.ValueKind != JsonValueKind.Object)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                Add(issues, path,
                    "mortal_wound_treatment_persisted_history_result_invalid",
                    "one strict closed treatment-result object",
                    element.ValueKind.ToString());
            }
            return false;
        }

        JsonObject result;
        try
        {
            result = JsonNode.Parse(element.GetRawText())?.AsObject() ??
                     throw new JsonException("The history result is empty.");
        }
        catch (Exception exception) when (exception is JsonException or
                                           InvalidOperationException)
        {
            Add(issues, path,
                "mortal_wound_treatment_persisted_history_result_invalid",
                "one strict closed treatment-result object",
                exception.GetType().Name);
            return false;
        }

        if (result["requestAuthority"] is not JsonObject requestNode ||
            requestNode["coordinates"] is not JsonObject coordinatesNode)
        {
            Add(issues, path + ".requestAuthority",
                "mortal_wound_treatment_persisted_history_result_invalid",
                "one complete request authority with coordinates",
                "missing or not an object");
            return false;
        }

        const string fallbackIdentifier = "invalid_history_treatment_coordinate";
        var fallbackFingerprint = "sha256:" + new string('0', 64);
        var sessionId = ReadString(coordinatesNode, "sessionId") ?? fallbackIdentifier;
        var requestId = ReadString(coordinatesNode, "requestId") ?? fallbackIdentifier;
        var snapshotToken = ReadString(coordinatesNode, "snapshotToken") ?? fallbackIdentifier;
        var operationKey = ReadString(coordinatesNode, "operationKey") ?? fallbackIdentifier;
        var requestFingerprint = ReadString(requestNode, "requestFingerprint") ??
                                 fallbackFingerprint;
        var resultFingerprint = ReadString(result, "resultFingerprint") ??
                                fallbackFingerprint;
        const string syntheticSummary =
            "Accepted Mortal wound treatment retained for durable replay.";
        var syntheticCommand = new JsonObject
        {
            ["kind"] = "accepted_transition",
            ["transitionKind"] = "treat",
            ["commandRef"] = ComputeCommandRef(
                sessionId,
                requestId,
                snapshotToken,
                operationKey,
                requestFingerprint,
                resultFingerprint,
                syntheticSummary),
            ["operationKey"] = operationKey,
            ["authority"] = new JsonObject
            {
                ["request"] = requestNode.DeepClone()
            },
            ["result"] = result.DeepClone(),
            ["finalSceneText"] = syntheticSummary
        };
        var parseIssues = new List<ValidationIssue>();
        var draft = Parse(
            JsonSerializer.SerializeToElement(syntheticCommand),
            0,
            sessionId,
            requestId,
            snapshotToken,
            parseIssues);
        foreach (var issue in parseIssues)
            issues.Add(RebaseHistoryIssue(issue, path));
        if (draft is null || parseIssues.Count != 0 ||
            !TryParseRequest(
                draft.Request,
                path + ".requestAuthority",
                issues,
                out request) ||
            request is null ||
            !TryRestoreHistoryReceipt(result, request, path, issues, out receipt) ||
            receipt is null)
        {
            request = null;
            receipt = null;
            return false;
        }

        canonicalResult = WoundResponseInputComposer
            .ComposeMortalWoundTreatmentPersistedResult(request, receipt);
        if (!SemanticJsonEquals(result, canonicalResult))
        {
            Add(issues, path,
                "mortal_wound_treatment_persisted_history_result_roundtrip_mismatch",
                "one exact result graph preserved by typed reconstruction",
                "serialized fields were ignored, defaulted, or normalized");
        }
        if (issues.Count != start)
        {
            request = null;
            receipt = null;
            canonicalResult = null;
            return false;
        }
        return true;
    }

    private static bool TryRestoreHistoryReceipt(
        JsonObject result,
        MortalWoundTreatmentAttemptRequest request,
        string path,
        ICollection<ValidationIssue> issues,
        out MortalWoundTreatmentReceipt? receipt)
    {
        receipt = null;
        var start = issues.Count;
        try
        {
            if (result["modeEvidence"] is not JsonObject evidenceNode)
                throw new JsonException("The typed mode evidence is missing.");
            MortalWoundTreatmentModeEvidence? modeEvidence = request.Mode switch
            {
                "procedure" => Deserialize<MortalWoundProcedureModeEvidence>(evidenceNode),
                "course" => Deserialize<MortalWoundCourseModeEvidence>(evidenceNode),
                "guaranteed" => Deserialize<MortalWoundGuaranteedModeEvidence>(evidenceNode),
                _ => null
            };
            var declaredResult = ParseOperations(
                result,
                "declaredResult",
                path,
                issues);
            var resultCategory = ReadClosedString(
                result,
                "resultCategory",
                path,
                issues,
                "success", "partial_success", "failed_attempt");
            var selectedOutcomeIndex = ReadNullableNonNegativeInt32(
                result,
                "selectedOutcomeIndex",
                path,
                issues);
            var interruption = ReadBoolean(result, "interruption", path, issues);
            var consumptionTrigger = ReadClosedString(
                result,
                "consumptionTrigger",
                path,
                issues,
                "success", "partial_success", "failed_attempt", "none");
            var routeFingerprint = ReadFingerprint(
                result,
                "routeFingerprint",
                path,
                issues);
            var resolutionFingerprint = ReadFingerprint(
                result,
                "resolutionAuthorityFingerprint",
                path,
                issues);
            var resultFingerprint = ReadFingerprint(
                result,
                "resultFingerprint",
                path,
                issues);
            var receiptFingerprint = ReadFingerprint(
                result,
                "receiptFingerprint",
                path,
                issues);
            var routeCompletionToken = ReadClosedString(
                result,
                "routeCompletion",
                path,
                issues,
                "append_once", "none");
            var courseAuthority = request.ModeAuthority as MortalWoundCourseModeAuthority;
            var courseDisposition = modeEvidence is MortalWoundCourseModeEvidence courseEvidence
                ? courseEvidence.CourseDisposition
                : null;
            if (issues.Count != start || modeEvidence is null || declaredResult is null ||
                resultCategory is null || consumptionTrigger is null ||
                routeFingerprint is null || resolutionFingerprint is null ||
                resultFingerprint is null || receiptFingerprint is null ||
                routeCompletionToken is null)
            {
                return false;
            }

            receipt = MortalWoundTreatmentReceipt.RestorePersisted(
                request,
                resultCategory,
                selectedOutcomeIndex,
                interruption,
                declaredResult,
                consumptionTrigger,
                courseAuthority?.CourseId,
                courseAuthority?.MilestoneOrdinal,
                courseDisposition,
                modeEvidence,
                routeFingerprint,
                resolutionFingerprint,
                resultFingerprint,
                routeCompletionToken == "append_once" ? "AppendOnce" : "None",
                receiptFingerprint);
            if (receipt is null)
            {
                Add(issues, path,
                    "mortal_wound_treatment_persisted_history_receipt_mismatch",
                    "one detached receipt recomputed from the complete result and request",
                    "receipt reconstruction failed");
            }
        }
        catch (Exception exception) when (exception is JsonException or
                                           NotSupportedException or
                                           InvalidOperationException or
                                           ArgumentException or
                                           OverflowException or
                                           NullReferenceException)
        {
            Add(issues, path,
                "mortal_wound_treatment_persisted_history_receipt_mismatch",
                "one detached receipt recomputed from the complete result and request",
                exception.GetType().Name + ": " + exception.Message);
            receipt = null;
        }
        return issues.Count == start && receipt is not null;
    }

    private static ValidationIssue RebaseHistoryIssue(
        ValidationIssue issue,
        string resultPath)
    {
        var resultMarker = ".result";
        var requestMarker = ".authority.request";
        string rebased;
        var marker = issue.FilePath.IndexOf(resultMarker, StringComparison.Ordinal);
        if (marker >= 0)
        {
            rebased = resultPath + issue.FilePath[(marker + resultMarker.Length)..];
        }
        else
        {
            marker = issue.FilePath.IndexOf(requestMarker, StringComparison.Ordinal);
            rebased = marker >= 0
                ? resultPath + ".requestAuthority" +
                  issue.FilePath[(marker + requestMarker.Length)..]
                : resultPath;
        }
        return new ValidationIssue(
            rebased,
            issue.Severity,
            issue.Message,
            issue.Code,
            issue.Actor,
            issue.Section,
            issue.Expected,
            issue.Actual,
            issue.RepairHint,
            issue.Category);
    }

    private static bool HasMatchingResultSemantics(
        MortalWoundTreatmentAttemptRequest request,
        string resultCategory,
        int? selectedOutcomeIndex,
        bool interruption,
        string consumptionTrigger,
        string? courseDisposition,
        string routeFingerprint,
        string routeCompletion)
    {
        if (!TryDeriveExpectedResult(
                request,
                out var expectedCategory,
                out var expectedIndex,
                out var expectedInterruption,
                out var expectedDisposition,
                out var expectedCompletion,
                out _) ||
            !string.Equals(
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

        var expectedConsumptionTrigger = !expectedInterruption &&
                                         request.ResourceAuthority.Policy.ConsumeOn.Contains(
                                             expectedCategory,
                                             StringComparer.Ordinal)
            ? expectedCategory
            : "none";
        return string.Equals(
                   resultCategory,
                   expectedCategory,
                   StringComparison.Ordinal) &&
               selectedOutcomeIndex == expectedIndex &&
               interruption == expectedInterruption &&
               string.Equals(
                   consumptionTrigger,
                   expectedConsumptionTrigger,
                   StringComparison.Ordinal) &&
               string.Equals(
                   courseDisposition,
                   expectedDisposition,
                   StringComparison.Ordinal) &&
               string.Equals(
                   routeCompletion,
                   expectedCompletion,
                   StringComparison.Ordinal);
    }

    private static bool HasMatchingDeclaredResult(
        MortalWoundTreatmentAttemptRequest request,
        IReadOnlyList<MortalWoundTreatmentOperation> declaredResult)
    {
        if (!TryDeriveExpectedResult(
                request,
                out _,
                out _,
                out _,
                out _,
                out _,
                out var expected) ||
            declaredResult.Count != expected.Count)
        {
            return false;
        }

        for (var index = 0; index < expected.Count; index++)
        {
            var actualNode = WoundResponseInputComposer
                .SerializeMortalWoundTreatmentValue(declaredResult[index]) as JsonObject;
            var expectedNode = WoundResponseInputComposer
                .SerializeMortalWoundTreatmentValue(expected[index]) as JsonObject;
            if (actualNode is null || expectedNode is null ||
                !SemanticJsonEquals(actualNode, expectedNode))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryDeriveExpectedResult(
        MortalWoundTreatmentAttemptRequest request,
        out string resultCategory,
        out int? selectedOutcomeIndex,
        out bool interruption,
        out string? courseDisposition,
        out string routeCompletion,
        out IReadOnlyList<MortalWoundTreatmentOperation> declaredResult)
    {
        resultCategory = string.Empty;
        selectedOutcomeIndex = null;
        interruption = false;
        courseDisposition = null;
        routeCompletion = "None";
        declaredResult = Array.Empty<MortalWoundTreatmentOperation>();
        if (!MortalWoundTreatmentDetachedSealValidator.TryGetRoute(
                request,
                out var selectedRoute) ||
            selectedRoute is null)
        {
            return false;
        }

        switch (request.ModeAuthority, selectedRoute)
        {
            case (MortalWoundProcedureCheckAuthority authority,
                MortalWoundProcedureRouteDefinition route):
            {
                long total;
                long margin;
                try
                {
                    total = checked((long)authority.NaturalRoll + authority.Modifier);
                    margin = checked(total - authority.EffectiveDifficulty);
                }
                catch (OverflowException)
                {
                    return false;
                }
                var reacted = authority.NaturalRoll == 1 &&
                              authority.PreparedCriticalReaction is not null;
                var index = MortalWoundTreatmentPlanner.SelectProcedureBand(
                    route,
                    authority.NaturalRoll,
                    margin,
                    reacted);
                if (index < 0 || index >= route.Bands.Length)
                    return false;
                resultCategory = route.Bands[index].Category;
                selectedOutcomeIndex = index;
                declaredResult = route.Bands[index].DeclaredResult;
                break;
            }
            case (MortalWoundCourseModeAuthority authority,
                MortalWoundCourseRouteDefinition route):
            {
                var milestones = route.Milestones.Where(candidate =>
                    candidate.Ordinal == authority.MilestoneOrdinal).ToArray();
                if (milestones.Length != 1)
                    return false;
                interruption = request.RequirementAuthority.InterruptionReason is not null;
                resultCategory = interruption
                    ? route.Interruption.Category
                    : milestones[0].Category;
                selectedOutcomeIndex = interruption
                    ? null
                    : authority.MilestoneOrdinal - 1;
                courseDisposition = interruption
                    ? "interrupted"
                    : milestones[0].Completion;
                declaredResult = interruption
                    ? route.Interruption.DeclaredResult
                    : milestones[0].DeclaredResult;
                break;
            }
            case (MortalWoundTreatmentCapabilityProof,
                MortalWoundGuaranteedRouteDefinition route):
                resultCategory = route.Outcome.Category;
                selectedOutcomeIndex = 0;
                declaredResult = route.Outcome.DeclaredResult;
                break;
            default:
                return false;
        }

        var earnsCompletion = !interruption &&
                              string.Equals(
                                  resultCategory,
                                  "success",
                                  StringComparison.Ordinal) &&
                              (request.Mode != "course" ||
                               string.Equals(
                                   courseDisposition,
                                   "completed",
                                   StringComparison.Ordinal));
        routeCompletion = earnsCompletion &&
                          !request.RouteSourceWound.Treatment.CompletedRouteIds.Contains(
                              request.Coordinates.RouteId,
                              StringComparer.Ordinal)
            ? "AppendOnce"
            : "None";
        return true;
    }

    internal static bool MatchesRootBinding(
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string? sessionId,
        string? requestId,
        string? snapshotToken) =>
        string.Equals(coordinates.SessionId, sessionId, StringComparison.Ordinal) &&
        string.Equals(coordinates.RequestId, requestId, StringComparison.Ordinal) &&
        string.Equals(
            coordinates.SnapshotToken,
            snapshotToken,
            StringComparison.Ordinal);

    internal static string ComputeCommandRef(
        string sessionId,
        string requestId,
        string snapshotToken,
        string operationKey,
        string requestFingerprint,
        string resultFingerprint,
        string finalSceneText) =>
        "mortal_wound_treatment_command_" +
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.command",
            "1",
            sessionId,
            requestId,
            snapshotToken,
            operationKey,
            requestFingerprint,
            resultFingerprint,
            finalSceneText
        })["sha256:".Length..];

    private static bool TryValidateModeEvidence(
        JsonObject source,
        MortalWoundTreatmentAttemptRequest request,
        int? selectedOutcomeIndex,
        string path,
        ICollection<ValidationIssue> issues,
        out string? evidenceFingerprint,
        out string? reactionFingerprint,
        out string? courseDisposition)
    {
        evidenceFingerprint = null;
        reactionFingerprint = null;
        courseDisposition = null;
        try
        {
            MortalWoundTreatmentModeEvidence? evidence = request.Mode switch
            {
                "procedure" => Deserialize<MortalWoundProcedureModeEvidence>(source),
                "course" => Deserialize<MortalWoundCourseModeEvidence>(source),
                "guaranteed" => Deserialize<MortalWoundGuaranteedModeEvidence>(source),
                _ => null
            };
            if (evidence is null)
                throw new JsonException("One typed mode-evidence graph is required.");

            var serialized = JsonSerializer.SerializeToNode(
                evidence,
                evidence.GetType(),
                RequestJsonOptions) as JsonObject;
            if (serialized is null || !SemanticJsonEquals(source, serialized))
            {
                Add(issues, path,
                    "mortal_wound_treatment_persisted_mode_evidence_mismatch",
                    "one exact closed typed mode-evidence graph",
                    "serialized fields do not survive typed reconstruction");
                return false;
            }

            var valid = (request.ModeAuthority, evidence) switch
            {
                (MortalWoundProcedureCheckAuthority authority,
                    MortalWoundProcedureModeEvidence procedure) =>
                    HasMatchingProcedureEvidence(
                        request,
                        authority,
                        procedure,
                        selectedOutcomeIndex,
                        out evidenceFingerprint,
                        out reactionFingerprint),
                (MortalWoundCourseModeAuthority authority,
                    MortalWoundCourseModeEvidence course) =>
                    HasMatchingCourseEvidence(
                        request,
                        authority,
                        course,
                        selectedOutcomeIndex,
                        out evidenceFingerprint,
                        out courseDisposition),
                (MortalWoundTreatmentCapabilityProof proof,
                    MortalWoundGuaranteedModeEvidence guaranteed) =>
                    HasMatchingGuaranteedEvidence(
                        request,
                        proof,
                        guaranteed,
                        out evidenceFingerprint),
                _ => false
            };
            if (valid)
                return true;
        }
        catch (Exception exception) when (exception is JsonException or
                                           NotSupportedException or
                                           InvalidOperationException or
                                           ArgumentException or
                                           OverflowException or
                                           NullReferenceException)
        {
            Add(issues, path,
                "mortal_wound_treatment_persisted_mode_evidence_mismatch",
                "one independently recomputable mode-evidence graph",
                exception.GetType().Name + ": " + exception.Message);
            return false;
        }

        Add(issues, path,
            "mortal_wound_treatment_persisted_mode_evidence_mismatch",
            "mode evidence matching the complete sealed request authority",
            "one or more evidence fields disagree");
        evidenceFingerprint = null;
        reactionFingerprint = null;
        courseDisposition = null;
        return false;
    }

    private static bool HasMatchingProcedureEvidence(
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundProcedureCheckAuthority authority,
        MortalWoundProcedureModeEvidence evidence,
        int? selectedOutcomeIndex,
        out string? evidenceFingerprint,
        out string? reactionFingerprint)
    {
        evidenceFingerprint = evidence.AcceptedRollFingerprint;
        reactionFingerprint = evidence.ReactionFingerprint;
        long total;
        long margin;
        int baseDifficulty;
        try
        {
            total = checked((long)authority.NaturalRoll + authority.Modifier);
            margin = checked(total - authority.EffectiveDifficulty);
            baseDifficulty = checked(
                authority.EffectiveDifficulty -
                authority.ComplicationDifficultyModifier);
        }
        catch (OverflowException)
        {
            return false;
        }
        if (!MortalWoundTreatmentDetachedSealValidator.TryGetRoute(
                request,
                out var selectedRoute) ||
            selectedRoute is not MortalWoundProcedureRouteDefinition procedureRoute)
        {
            return false;
        }

        var originalOutcome = authority.NaturalRoll switch
        {
            20 => "critical_success",
            1 => "critical_failure",
            _ => "ordinary"
        };
        var resolvedOutcome = authority.NaturalRoll == 1 &&
                              evidence.ReactionFingerprint is not null
            ? "failure"
            : originalOutcome;
        var expectedSelectedIndex = MortalWoundTreatmentPlanner.SelectProcedureBand(
            procedureRoute,
            authority.NaturalRoll,
            margin,
            authority.NaturalRoll == 1 && evidence.ReactionFingerprint is not null);
        if (expectedSelectedIndex < 0 ||
            expectedSelectedIndex >= procedureRoute.Bands.Length)
        {
            return false;
        }
        var expectedReactionFingerprint = authority.PreparedCriticalReaction is { } prepared
            ? MortalWoundCriticalReactionIntent.Create(request, prepared).IntentFingerprint
            : null;
        var reactionShapeMatches = evidence.ReactionFingerprint is null
            ? evidence.ReactionEffectId is null &&
              evidence.ReactionTriggerId is null &&
              expectedReactionFingerprint is null
            : authority.PreparedCriticalReaction is { } preparedReaction &&
              string.Equals(
                  evidence.ReactionFingerprint,
                  expectedReactionFingerprint,
                  StringComparison.Ordinal) &&
              string.Equals(
                  evidence.ReactionEffectId,
                  preparedReaction.EffectId,
                  StringComparison.Ordinal) &&
              string.Equals(
                  evidence.ReactionTriggerId,
                  preparedReaction.TriggerId,
                  StringComparison.Ordinal);
        var expectedFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.procedure_evidence",
                "1",
                authority.AuthorityFingerprint,
                total.ToString(CultureInfo.InvariantCulture),
                baseDifficulty.ToString(CultureInfo.InvariantCulture),
                margin.ToString(CultureInfo.InvariantCulture),
                originalOutcome,
                resolvedOutcome,
                evidence.SelectedBandId,
                evidence.SelectedOutcomeIndex.ToString(CultureInfo.InvariantCulture),
                evidence.ReactionFingerprint
            });
        return string.Equals(evidence.RollMode, authority.RollMode,
                   StringComparison.Ordinal) &&
               string.Equals(evidence.RollActorKind, authority.RollActorKind,
                   StringComparison.Ordinal) &&
               string.Equals(evidence.RollActorId, authority.RollActorId,
                   StringComparison.Ordinal) &&
               evidence.SourceIndices.SequenceEqual(authority.SourceIndices) &&
               evidence.SourceRolls.SequenceEqual(authority.SourceRolls) &&
               evidence.SelectedSourceIndex == authority.SelectedSourceIndex &&
               evidence.NaturalRoll == authority.NaturalRoll &&
               evidence.Modifier == authority.Modifier &&
               evidence.Total == total &&
               evidence.BaseDifficulty == baseDifficulty &&
               evidence.ComplicationDifficultyModifier ==
               authority.ComplicationDifficultyModifier &&
               evidence.EffectiveDifficulty == authority.EffectiveDifficulty &&
               evidence.Margin == margin &&
               string.Equals(evidence.OriginalOutcome, originalOutcome,
                   StringComparison.Ordinal) &&
               string.Equals(evidence.ResolvedOutcome, resolvedOutcome,
                   StringComparison.Ordinal) &&
               string.Equals(
                   evidence.SelectedBandId,
                   procedureRoute.Bands[expectedSelectedIndex].BandId,
                   StringComparison.Ordinal) &&
               evidence.SelectedOutcomeIndex == expectedSelectedIndex &&
               evidence.SelectedOutcomeIndex == selectedOutcomeIndex &&
               reactionShapeMatches &&
               string.Equals(
                   evidence.AcceptedRollFingerprint,
                   expectedFingerprint,
                   StringComparison.Ordinal);
    }

    private static bool HasMatchingCourseEvidence(
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundCourseModeAuthority authority,
        MortalWoundCourseModeEvidence evidence,
        int? selectedOutcomeIndex,
        out string? evidenceFingerprint,
        out string? courseDisposition)
    {
        evidenceFingerprint = evidence.ClockEvidenceFingerprint;
        courseDisposition = evidence.CourseDisposition;
        if (!MortalWoundTreatmentDetachedSealValidator.TryGetRoute(
                request,
                out var selectedRoute) ||
            selectedRoute is not MortalWoundCourseRouteDefinition route)
        {
            return false;
        }
        var milestones = route.Milestones.Where(candidate =>
            candidate.Ordinal == authority.MilestoneOrdinal).ToArray();
        if (milestones.Length != 1)
            return false;
        var interrupted = request.RequirementAuthority.InterruptionReason is not null;
        var expectedDisposition = interrupted
            ? "interrupted"
            : milestones[0].Completion;
        var expectedOutcomeIndex = interrupted
            ? (int?)null
            : authority.MilestoneOrdinal - 1;
        var expectedFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
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
                evidence.CourseDisposition
            });
        return string.Equals(evidence.CourseDisposition, expectedDisposition,
                   StringComparison.Ordinal) &&
               selectedOutcomeIndex == expectedOutcomeIndex &&
               string.Equals(evidence.CourseId, authority.CourseId,
                   StringComparison.Ordinal) &&
               evidence.MilestoneOrdinal == authority.MilestoneOrdinal &&
               evidence.CourseStartedAtGameTimeMinutes ==
               authority.CourseStartAuthority.StartedAtGameTimeMinutes &&
               evidence.ResolvedAtGameTimeMinutes ==
               authority.GameTimeAuthority.CurrentTimeInMinutes &&
               string.Equals(
                   evidence.ClockEvidenceFingerprint,
                   expectedFingerprint,
                   StringComparison.Ordinal);
    }

    private static bool HasMatchingGuaranteedEvidence(
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentCapabilityProof proof,
        MortalWoundGuaranteedModeEvidence evidence,
        out string? evidenceFingerprint)
    {
        evidenceFingerprint = evidence.CapabilityProofFingerprint;
        if (!MortalWoundTreatmentDetachedSealValidator.TryGetRoute(
                request,
                out var selectedRoute) ||
            selectedRoute is not MortalWoundGuaranteedRouteDefinition route ||
            !string.Equals(
                evidence.ActorRole,
                route.Resolution.ActorRole,
                StringComparison.Ordinal))
        {
            return false;
        }
        var coordinates = request.Coordinates;
        var actorMatches = evidence.ActorRole switch
        {
            "provider" =>
                string.Equals(
                    proof.OwnerKind,
                    coordinates.ProviderKind,
                    StringComparison.Ordinal) &&
                string.Equals(
                    proof.OwnerId,
                    coordinates.ProviderId,
                    StringComparison.Ordinal),
            "target" =>
                string.Equals(
                    proof.OwnerKind,
                    coordinates.TargetKind,
                    StringComparison.Ordinal) &&
                string.Equals(
                    proof.OwnerId,
                    coordinates.TargetId,
                    StringComparison.Ordinal),
            _ => false
        };
        return actorMatches &&
               string.Equals(evidence.CapabilityRef, proof.CapabilityRef,
                   StringComparison.Ordinal) &&
               string.Equals(evidence.SkillId, proof.SkillId,
                   StringComparison.Ordinal) &&
               string.Equals(
                   evidence.SourceSemanticFingerprint,
                   proof.SourceSemanticFingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(
                   evidence.CapabilityProofFingerprint,
                   proof.ProofFingerprint,
                   StringComparison.Ordinal);
    }

    internal static bool TryParseRequest(
        JsonObject source,
        string path,
        ICollection<ValidationIssue> issues,
        out MortalWoundTreatmentAttemptRequest? request)
    {
        request = null;
        var start = issues.Count;
        if (!TryClosedObject(source, RequestFields, path, issues, out var value))
            return false;

        var mode = ReadClosedString(
            value,
            "mode",
            path,
            issues,
            "procedure",
            "course",
            "guaranteed");
        var requestFingerprint = ReadFingerprint(
            value,
            "requestFingerprint",
            path,
            issues);
        var routeSourceWoundFingerprint = ReadFingerprint(
            value,
            "routeSourceWoundFingerprint",
            path,
            issues);
        int? milestoneOrdinal = null;
        if (!value.TryGetPropertyValue("milestoneOrdinal", out var milestoneNode))
        {
            Add(issues, path + ".milestoneOrdinal", "wound_command_missing_field",
                "positive integer or null", "missing");
        }
        else if (milestoneNode is JsonValue milestoneValue &&
                 milestoneValue.TryGetValue<int>(out var parsedOrdinal) &&
                 parsedOrdinal > 0)
        {
            milestoneOrdinal = parsedOrdinal;
        }
        else if (milestoneNode is not null)
        {
            Add(issues, path + ".milestoneOrdinal", "wound_command_invalid_field",
                "positive integer or null", milestoneNode.GetValueKind().ToString());
        }

        if (!TrySingleObject(value, "coordinates", path, issues, out var coordinatesNode) ||
            !TrySingleObject(value, "routeSourceWound", path, issues,
                out var routeSourceWoundNode) ||
            !TrySingleObject(value, "modeAuthority", path, issues, out var modeAuthorityNode) ||
            !TrySingleObject(value, "requirementAuthority", path, issues,
                out var requirementNode) ||
            !TrySingleObject(value, "resourceAuthority", path, issues,
                out var resourceNode))
        {
            return false;
        }

        if (mode is not null &&
            ((mode == "course") != milestoneOrdinal.HasValue))
        {
            Add(issues, path + ".milestoneOrdinal",
                "mortal_wound_treatment_persisted_request_mode_mismatch",
                mode == "course" ? "one positive course milestone" : "null",
                milestoneOrdinal?.ToString() ?? "null");
        }

        try
        {
            var coordinates = Deserialize<MortalWoundTreatmentAttemptCoordinates>(
                coordinatesNode);
            var routeSourceResult = WoundMaterializationContract.Parse(
                routeSourceWoundNode.ToJsonString(),
                path + ".routeSourceWound");
            var routeSourceWound = routeSourceResult.IsValid
                ? routeSourceResult.Wound
                : null;
            MortalWoundTreatmentModeAuthority? modeAuthority = mode switch
            {
                "procedure" => Deserialize<MortalWoundProcedureCheckAuthority>(
                    modeAuthorityNode),
                "course" => DeserializeCourseModeAuthority(modeAuthorityNode),
                "guaranteed" => Deserialize<MortalWoundTreatmentCapabilityProof>(
                    modeAuthorityNode),
                _ => null
            };
            var requirementAuthority =
                Deserialize<MortalWoundTreatmentRequirementAuthorityBundle>(
                    requirementNode);
            var resourceAuthority =
                Deserialize<MortalWoundTreatmentResourceReservationAuthority>(
                    resourceNode);
            if (coordinates is null || routeSourceWound is null ||
                routeSourceWoundFingerprint is null || modeAuthority is null ||
                requirementAuthority is null || resourceAuthority is null ||
                requestFingerprint is null || mode is null)
            {
                throw new JsonException("The complete typed request graph is required.");
            }

            request = MortalWoundTreatmentAttemptRequest.RestoreDetached(
                mode,
                coordinates,
                milestoneOrdinal,
                routeSourceWound,
                routeSourceWoundFingerprint,
                modeAuthority,
                requirementAuthority,
                resourceAuthority,
                requestFingerprint);
            if (request is not null)
            {
                var roundTrip = WoundResponseInputComposer
                    .SerializeMortalWoundTreatmentValue(request) as JsonObject;
                if (roundTrip is null || !SemanticJsonEquals(source, roundTrip))
                {
                    Add(issues, path,
                        "mortal_wound_treatment_persisted_request_roundtrip_mismatch",
                        "one exact request graph preserved by typed reconstruction",
                        "serialized fields were ignored, defaulted, or normalized");
                    request = null;
                }
            }
            var sealMismatch = request is null
                ? "request"
                : MortalWoundTreatmentDetachedSealValidator.FindMismatch(request);
            if (sealMismatch is not null)
            {
                Add(issues, path,
                    "mortal_wound_treatment_persisted_request_seal_mismatch",
                    "one recursively recomputable complete treatment request seal",
                    sealMismatch + " carried authority fingerprint disagrees");
                request = null;
            }
        }
        catch (Exception exception) when (exception is JsonException or
                                           NotSupportedException or
                                           InvalidOperationException or
                                           ArgumentException or
                                           OverflowException or
                                           NullReferenceException)
        {
            Add(issues, path,
                "mortal_wound_treatment_persisted_request_invalid",
                "one closed typed treatment request graph",
                exception.GetType().Name + ": " + exception.Message);
            request = null;
        }

        return issues.Count == start && request is not null;
    }

    internal static JsonObject? RecomposeRoot(
        WoundAcceptedTurnBinding binding,
        IReadOnlyList<MortalWoundTreatmentCommandDraft> commands)
    {
        if (commands.Count is < 1 or
                > WoundResponseInputComposer.MaximumAcceptedCommandCount)
            return null;
        for (var index = 0; index < commands.Count; index++)
        {
            var command = commands[index];
            var issues = new List<ValidationIssue>();
            var reparsed = Parse(
                JsonSerializer.SerializeToElement(command.Command),
                index,
                binding.SessionId,
                binding.RequestId,
                binding.SnapshotToken,
                issues);
            if (reparsed is null || issues.Count != 0 ||
                !SemanticJsonEquals(reparsed.Command, command.Command) ||
                !SemanticJsonEquals(reparsed.Request, command.Request) ||
                !string.Equals(reparsed.SessionId, command.SessionId,
                    StringComparison.Ordinal) ||
                !string.Equals(reparsed.RequestId, command.RequestId,
                    StringComparison.Ordinal) ||
                !string.Equals(reparsed.SnapshotToken, command.SnapshotToken,
                    StringComparison.Ordinal) ||
                !string.Equals(reparsed.OperationKey, command.OperationKey,
                    StringComparison.Ordinal) ||
                !string.Equals(reparsed.AttemptId, command.AttemptId,
                    StringComparison.Ordinal) ||
                !string.Equals(reparsed.EventRef, command.EventRef,
                    StringComparison.Ordinal) ||
                !string.Equals(reparsed.RequestFingerprint, command.RequestFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(reparsed.CourseId, command.CourseId,
                    StringComparison.Ordinal) ||
                reparsed.CourseMilestoneOrdinal != command.CourseMilestoneOrdinal ||
                !string.Equals(reparsed.FinalSceneText, command.FinalSceneText,
                    StringComparison.Ordinal) ||
                !TryParseRequest(
                    command.Request,
                    AcceptedMechanicsPlan.WoundCommandPath + ".authority.request",
                    issues,
                    out var request) ||
                request is null ||
                !WoundResponseInputComposer.MatchesAcceptedBinding(
                    binding,
                    request.Coordinates))
            {
                return null;
            }
        }
        if (!HasUniquePersistedCoordinates(commands))
            return null;
        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["sessionId"] = binding.SessionId,
            ["requestId"] = binding.RequestId,
            ["snapshotToken"] = binding.SnapshotToken,
            ["commands"] = new JsonArray(commands
                .Select(static value => (JsonNode?)value.Command.DeepClone())
                .ToArray())
        };
    }

    internal static bool HasUniquePersistedCoordinates(
        IReadOnlyList<MortalWoundTreatmentCommandDraft> commands) =>
        HasUniquePersistedCoordinates(commands
            .Select(static command => (
                command.OperationKey,
                command.AttemptId,
                command.EventRef,
                command.RequestFingerprint,
                command.CourseId,
                command.CourseMilestoneOrdinal))
            .ToArray());

    internal static bool HasUniquePersistedCoordinates(
        IReadOnlyList<(
            string OperationKey,
            string AttemptId,
            string EventRef,
            string RequestFingerprint,
            string? CourseId,
            int? CourseMilestoneOrdinal)> coordinates)
    {
        if (coordinates.Count is < 1 or
                > WoundResponseInputComposer.MaximumAcceptedCommandCount)
            return false;
        var operationKeys = new HashSet<string>(StringComparer.Ordinal);
        var attemptIds = new HashSet<string>(StringComparer.Ordinal);
        var eventRefs = new HashSet<string>(StringComparer.Ordinal);
        var requestFingerprints = new HashSet<string>(StringComparer.Ordinal);
        var courseCoordinates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var coordinate in coordinates)
        {
            if (!TryAddConfusableCoordinate(
                    operationKeys,
                    coordinate.OperationKey) ||
                !TryAddConfusableCoordinate(
                    attemptIds,
                    coordinate.AttemptId) ||
                !TryAddConfusableCoordinate(
                    eventRefs,
                    coordinate.EventRef) ||
                string.IsNullOrWhiteSpace(coordinate.RequestFingerprint) ||
                !requestFingerprints.Add(coordinate.RequestFingerprint) ||
                (coordinate.CourseId is null) !=
                    (coordinate.CourseMilestoneOrdinal is null))
            {
                return false;
            }
            if (coordinate.CourseId is not null &&
                (!ResourceMaterializationContract.IsExactIdentifier(
                     coordinate.CourseId) ||
                 coordinate.CourseMilestoneOrdinal is not > 0 ||
                 !courseCoordinates.Add(
                     ExactIdentifierConfusableKey.Build(coordinate.CourseId) +
                     "\u001f" +
                     coordinate.CourseMilestoneOrdinal.Value.ToString(
                         CultureInfo.InvariantCulture))))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryAddConfusableCoordinate(
        ISet<string> coordinates,
        string value) =>
        ResourceMaterializationContract.IsExactIdentifier(value) &&
        coordinates.Add(ExactIdentifierConfusableKey.Build(value));

    internal static bool SemanticJsonEquals(JsonObject left, JsonObject right) =>
        CanonicalNode(left) == CanonicalNode(right);

    internal static bool HasNoDuplicateProperties(
        JsonElement value,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var valid = true;
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    Add(
                        issues,
                        path + "." + property.Name,
                        "wound_command_duplicate_field",
                        "one exact property",
                        property.Name);
                    valid = false;
                }
                if (!HasNoDuplicateProperties(
                        property.Value,
                        path + "." + property.Name,
                        issues))
                {
                    valid = false;
                }
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                if (!HasNoDuplicateProperties(item, $"{path}[{index++}]", issues))
                    valid = false;
            }
        }
        return valid;
    }

    private static string CanonicalNode(JsonNode? node) => node switch
    {
        JsonObject value => "{" + string.Join(",", value
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => JsonSerializer.Serialize(pair.Key) + ":" +
                            CanonicalNode(pair.Value))) + "}",
        JsonArray value => "[" + string.Join(",", value.Select(CanonicalNode)) + "]",
        null => "null",
        _ => node.ToJsonString()
    };

    private static IReadOnlyList<MortalWoundTreatmentOperation>? ParseOperations(
        JsonObject source,
        string property,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (source[property] is not JsonArray array)
        {
            Add(issues, path + "." + property,
                "mortal_wound_treatment_persisted_result_invalid",
                "one ordered declared-result array",
                source[property]?.GetValueKind().ToString() ?? "missing");
            return null;
        }
        if (array.Count > MortalWoundTreatmentContract.MaxOutcomeOperations)
        {
            Add(issues, path + "." + property,
                "mortal_wound_treatment_persisted_result_invalid",
                $"at most {MortalWoundTreatmentContract.MaxOutcomeOperations} ordered operations",
                array.Count.ToString(CultureInfo.InvariantCulture));
            return null;
        }
        var result = new List<MortalWoundTreatmentOperation>(array.Count);
        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is not JsonObject operation ||
                operation["kind"] is not JsonValue kindValue ||
                !kindValue.TryGetValue<string>(out var kind))
            {
                Add(issues, $"{path}.{property}[{index}]",
                    "mortal_wound_treatment_persisted_result_invalid",
                    "one typed treatment operation",
                    "missing kind");
                return null;
            }
            try
            {
                var json = operation.ToJsonString();
                MortalWoundTreatmentOperation? parsed = kind switch
                {
                    "no_improvement" => JsonSerializer.Deserialize<MortalWoundNoImprovementOperation>(json, OperationJsonOptions),
                    "stabilize" => JsonSerializer.Deserialize<MortalWoundStabilizeOperation>(json, OperationJsonOptions),
                    "add_recovery" => JsonSerializer.Deserialize<MortalWoundAddRecoveryOperation>(json, OperationJsonOptions),
                    "reduce_severity" => JsonSerializer.Deserialize<MortalWoundReduceSeverityOperation>(json, OperationJsonOptions),
                    "remove_complication" => JsonSerializer.Deserialize<MortalWoundRemoveComplicationOperation>(json, OperationJsonOptions),
                    "add_complication" => JsonSerializer.Deserialize<MortalWoundAddComplicationOperation>(json, OperationJsonOptions),
                    "apply_deterioration" => JsonSerializer.Deserialize<MortalWoundApplyDeteriorationOperation>(json, OperationJsonOptions),
                    "heal" => JsonSerializer.Deserialize<MortalWoundHealOperation>(json, OperationJsonOptions),
                    _ => null
                };
                if (parsed is null)
                    throw new JsonException("Unknown or empty operation.");
                result.Add(parsed);
            }
            catch (Exception exception) when (exception is JsonException or
                                               NotSupportedException or
                                               InvalidOperationException)
            {
                Add(issues, $"{path}.{property}[{index}]",
                    "mortal_wound_treatment_persisted_result_invalid",
                    "one closed typed treatment operation",
                    exception.GetType().Name);
                return null;
            }
        }
        return result;
    }

    private static T? Deserialize<T>(JsonObject source) =>
        JsonSerializer.Deserialize<T>(source.ToJsonString(), RequestJsonOptions);

    private static MortalWoundCourseModeAuthority? DeserializeCourseModeAuthority(
        JsonObject source)
    {
        if (source["courseStartAuthority"] is not JsonObject start ||
            start["startingWound"] is not JsonObject woundNode)
        {
            throw new JsonException("A complete course-start wound is required.");
        }
        var parsed = WoundMaterializationContract.Parse(
            woundNode.ToJsonString(),
            "treatmentAttempt.modeAuthority.courseStartAuthority.startingWound");
        if (!parsed.IsValid || parsed.Wound is null)
            throw new JsonException(
                "The persisted course-start wound is invalid: " +
                string.Join(",", parsed.Issues.Select(static issue =>
                    issue.Code + "@" + issue.FilePath)));
        var serializerSource = source.DeepClone().AsObject();
        if (serializerSource["courseStartAuthority"] is not JsonObject serializerStart ||
            serializerStart["startingWound"] is not JsonObject serializerWound ||
            serializerWound["consequences"] is not JsonObject serializerConsequences)
        {
            throw new JsonException("A complete course-start serialization shell is required.");
        }
        serializerConsequences.Remove("ownedEffectSources");
        var authority = Deserialize<MortalWoundCourseModeAuthority>(serializerSource);
        if (authority is null)
            throw new JsonException("The course-mode authority is empty.");
        return authority.RestoreDetachedStartingWound(parsed.Wound);
    }

    private static JsonSerializerOptions CreateRequestJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new MortalWoundTreatmentRequirementEvidenceJsonConverter());
        return options;
    }

    private static bool TryClosedObject(
        JsonElement element,
        IReadOnlySet<string> expected,
        string path,
        ICollection<ValidationIssue> issues,
        out JsonObject value)
    {
        value = new JsonObject();
        if (element.ValueKind != JsonValueKind.Object)
        {
            Add(issues, path, "wound_command_invalid_field", "strict object",
                element.ValueKind.ToString());
            return false;
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                Add(issues, path + "." + property.Name,
                    "wound_command_duplicate_field", "one exact property", property.Name);
                continue;
            }
            if (!expected.Contains(property.Name))
            {
                Add(issues, path + "." + property.Name,
                    "wound_command_unknown_field", "closed treatment command", property.Name);
                continue;
            }
            value[property.Name] = JsonNode.Parse(property.Value.GetRawText());
        }
        foreach (var required in expected)
        {
            if (!seen.Contains(required))
                Add(issues, path + "." + required,
                    "wound_command_missing_field", required, "missing");
        }
        return seen.SetEquals(expected);
    }

    private static bool TryClosedObject(
        JsonObject source,
        IReadOnlySet<string> expected,
        string path,
        ICollection<ValidationIssue> issues,
        out JsonObject value)
    {
        value = source;
        var names = source.Select(static pair => pair.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var name in names.Except(expected, StringComparer.Ordinal))
            Add(issues, path + "." + name, "wound_command_unknown_field",
                "closed treatment authority", name);
        foreach (var name in expected.Except(names, StringComparer.Ordinal))
            Add(issues, path + "." + name, "wound_command_missing_field", name, "missing");
        return names.SetEquals(expected);
    }

    private static bool TrySingleObject(
        JsonObject source,
        string property,
        string path,
        ICollection<ValidationIssue> issues,
        out JsonObject value)
    {
        value = source[property] as JsonObject ?? new JsonObject();
        if (source[property] is JsonObject)
            return true;
        Add(issues, path + "." + property, "wound_command_invalid_field",
            "strict object", source[property]?.GetValueKind().ToString() ?? "missing");
        return false;
    }

    private static string? ReadIdentifier(
        JsonObject source,
        string property,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var value = ReadString(source, property);
        if (ResourceMaterializationContract.IsExactIdentifier(value))
            return value;
        Add(issues, path + "." + property, "wound_command_invalid_field",
            "one exact identifier", value ?? "missing");
        return null;
    }

    private static string? ReadReadable(
        JsonObject source,
        string property,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var value = ReadString(source, property);
        if (!string.IsNullOrWhiteSpace(value) && value.Length <= 8_192)
            return value;
        Add(issues, path + "." + property, "wound_command_invalid_field",
            "non-empty bounded scene text", value ?? "missing");
        return null;
    }

    private static string? ReadFingerprint(
        JsonObject source,
        string property,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var value = ReadString(source, property);
        if (ResourceMaterializationContract.IsAuthorityFingerprint(value))
            return value;
        Add(issues, path + "." + property, "wound_command_invalid_fingerprint",
            "sha256 authority fingerprint", value ?? "missing");
        return null;
    }

    private static string? ReadNullableFingerprint(
        JsonObject source,
        string property,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (!source.TryGetPropertyValue(property, out var node))
        {
            Add(issues, path + "." + property, "wound_command_missing_field",
                "fingerprint or null", "missing");
            return null;
        }
        if (node is null)
            return null;
        return ReadFingerprint(source, property, path, issues);
    }

    private static string? ReadClosedString(
        JsonObject source,
        string property,
        string path,
        ICollection<ValidationIssue> issues,
        params string[] allowed)
    {
        var value = ReadString(source, property);
        if (value is not null && allowed.Contains(value, StringComparer.Ordinal))
            return value;
        Add(issues, path + "." + property, "wound_command_invalid_field",
            string.Join('|', allowed), value ?? "missing");
        return null;
    }

    private static int? ReadNullableNonNegativeInt32(
        JsonObject source,
        string property,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (!source.TryGetPropertyValue(property, out var node))
        {
            Add(issues, path + "." + property, "wound_command_missing_field",
                "non-negative integer or null", "missing");
            return null;
        }
        if (node is null)
            return null;
        if (node is JsonValue value && value.TryGetValue<int>(out var parsed) && parsed >= 0)
            return parsed;
        Add(issues, path + "." + property, "wound_command_invalid_field",
            "non-negative integer or null", node.GetValueKind().ToString());
        return null;
    }

    private static int? ReadPositiveInt32(
        JsonObject source,
        string property,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (source[property] is JsonValue value &&
            value.TryGetValue<int>(out var parsed) && parsed > 0)
            return parsed;
        Add(issues, path + "." + property, "wound_command_invalid_field",
            "positive integer", source[property]?.GetValueKind().ToString() ?? "missing");
        return null;
    }

    private static bool ReadBoolean(
        JsonObject source,
        string property,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (source[property] is JsonValue value && value.TryGetValue<bool>(out var parsed))
            return parsed;
        Add(issues, path + "." + property, "wound_command_invalid_field",
            "boolean", source[property]?.GetValueKind().ToString() ?? "missing");
        return false;
    }

    private static bool ExactString(JsonObject source, string property, string? expected) =>
        string.Equals(ReadString(source, property), expected, StringComparison.Ordinal);

    private static string? ReadString(JsonObject source, string property) =>
        source[property] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;

    private static void Add(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) => issues.Add(new ValidationIssue(
        path,
        IssueSeverity.Error,
        "The persisted Mortal wound-treatment command cannot be trusted.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual));
}

internal static partial class WoundResponseInputComposer
{
    private const int MaximumMortalWoundTreatmentFinalSceneTextLength = 8_192;

    private static readonly JsonSerializerOptions TreatmentPersistenceJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    internal static JsonObject ComposeMortalWoundTreatmentCommandRoot(
        WoundAcceptedTurnBinding binding,
        MortalWoundTreatmentResolution resolution,
        string finalSceneText)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentException.ThrowIfNullOrWhiteSpace(finalSceneText);
        if (finalSceneText.Length > MaximumMortalWoundTreatmentFinalSceneTextLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(finalSceneText),
                finalSceneText.Length,
                $"Mortal wound-treatment scene text is limited to {MaximumMortalWoundTreatmentFinalSceneTextLength} characters.");
        }
        if (!MatchesAcceptedBinding(binding, resolution.Coordinates))
        {
            throw new InvalidOperationException(
                "The treatment resolution does not belong to this accepted turn binding.");
        }
        var result = ComposeMortalWoundTreatmentPersistedResult(
            resolution.RequestAuthority,
            MortalWoundTreatmentReceipt.Create(resolution));
        var request = result["requestAuthority"]!.DeepClone().AsObject();
        var commandRef = MortalWoundTreatmentCommandCodec.ComputeCommandRef(
            binding.SessionId,
            binding.RequestId,
            binding.SnapshotToken,
            resolution.Coordinates.OperationKey,
            resolution.RequestFingerprint,
            resolution.ResultFingerprint,
            finalSceneText);
        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["sessionId"] = binding.SessionId,
            ["requestId"] = binding.RequestId,
            ["snapshotToken"] = binding.SnapshotToken,
            ["commands"] = new JsonArray(new JsonObject
            {
                ["kind"] = "accepted_transition",
                ["transitionKind"] = "treat",
                ["commandRef"] = commandRef,
                ["operationKey"] = resolution.Coordinates.OperationKey,
                ["authority"] = new JsonObject
                {
                    ["request"] = request
                },
                ["result"] = result,
                ["finalSceneText"] = finalSceneText
            })
        };
    }

    internal static JsonObject ComposeMortalWoundTreatmentPersistedResult(
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!string.Equals(request.RequestFingerprint, receipt.RequestFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(request.Coordinates.CoordinatesFingerprint,
                receipt.Coordinates.CoordinatesFingerprint, StringComparison.Ordinal) ||
            !receipt.HasMatchingFingerprint())
        {
            throw new InvalidOperationException(
                "The treatment receipt does not belong to the complete sealed request.");
        }

        var serializedRequest = SerializeTyped(request)?.AsObject() ??
                                throw new InvalidOperationException(
                                    "The sealed treatment request could not be serialized.");
        return new JsonObject
        {
            ["kind"] = "treat",
            ["mode"] = receipt.Mode,
            ["routeId"] = receipt.Coordinates.RouteId,
            ["attemptDisposition"] = ToContractToken(receipt.AttemptDisposition),
            ["resultCategory"] = receipt.ResultCategory,
            ["selectedOutcomeIndex"] = receipt.SelectedOutcomeIndex,
            ["interruption"] = receipt.Interruption,
            ["declaredResult"] = SerializeTyped(receipt.DeclaredResult),
            ["consumptionTrigger"] = receipt.ConsumptionTrigger,
            ["requestAuthority"] = serializedRequest,
            ["modeEvidence"] = SerializeTyped(receipt.ModeEvidence),
            ["routeFingerprint"] = receipt.RouteFingerprint,
            ["resolutionAuthorityFingerprint"] = receipt.ResolutionAuthorityFingerprint,
            ["resultFingerprint"] = receipt.ResultFingerprint,
            ["routeCompletion"] = ToContractToken(receipt.RouteCompletion),
            ["receiptFingerprint"] = receipt.ReceiptFingerprint
        };
    }

    internal static bool MatchesAcceptedBinding(
        WoundAcceptedTurnBinding binding,
        MortalWoundTreatmentAttemptCoordinates coordinates)
    {
        var acceptedEvents = binding.AcceptedEvents;
        return MortalWoundTreatmentCommandCodec.MatchesRootBinding(
                   coordinates,
                   binding.SessionId,
                   binding.RequestId,
                   binding.SnapshotToken) &&
               string.Equals(coordinates.Realm, binding.Realm,
                   StringComparison.Ordinal) &&
               coordinates.Turn == binding.Turn &&
               string.Equals(
                   binding.AcceptedEventsFingerprint,
                   WoundAcceptedEventSetFingerprint.Compute(acceptedEvents),
                   StringComparison.Ordinal) &&
               acceptedEvents.Count(candidate =>
                   string.Equals(candidate.EventRef, coordinates.EventRef,
                       StringComparison.Ordinal) &&
                   string.Equals(candidate.Kind, coordinates.EventKind,
                       StringComparison.Ordinal) &&
                   string.Equals(candidate.AuthorityId, coordinates.EventAuthorityId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       candidate.SemanticFingerprint,
                       coordinates.EventSemanticFingerprint,
                       StringComparison.Ordinal)) == 1;
    }

    internal static JsonNode? SerializeMortalWoundTreatmentValue(object? value) =>
        SerializeTyped(value);

    private static JsonNode? SerializeTyped(object? value)
    {
        if (value is null)
            return null;
        var node = JsonSerializer.SerializeToNode(
            value,
            value.GetType(),
            TreatmentPersistenceJsonOptions);
        if (value is MortalWoundTreatmentAttemptRequest request &&
            node is JsonObject requestNode)
        {
            requestNode["routeSourceWound"] = JsonNode.Parse(
                WoundMaterializationContract.SerializeCanonical(
                    request.RouteSourceWound));
            if (request.ModeAuthority is MortalWoundCourseModeAuthority course &&
                requestNode["modeAuthority"] is JsonObject modeNode &&
                modeNode["courseStartAuthority"] is JsonObject startNode)
            {
                startNode["startingWound"] = JsonNode.Parse(
                    WoundMaterializationContract.SerializeCanonical(
                        course.CourseStartAuthority.StartingWound));
            }
        }
        return node;
    }

    private static string ToContractToken(string value)
    {
        var characters = new List<char>(value.Length + 4);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsUpper(character) && index != 0)
                characters.Add('_');
            characters.Add(char.ToLowerInvariant(character));
        }
        return new string(characters.ToArray());
    }
}

internal static partial class WoundRepairPacketBuilder
{
    internal static JsonObject ComposePendingRoot(
        WoundAcceptedTurnBinding binding,
        IEnumerable<WoundRepairPacket> packets,
        WoundResponseCommandParsingResult parsedCommand)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(packets);
        ArgumentNullException.ThrowIfNull(parsedCommand);
        var packetArray = packets.Take(CandidateLimit + 1).ToArray();
        if (packetArray.Length > CandidateLimit ||
            packetArray.Any(static packet => packet is null))
        {
            throw new InvalidOperationException(
                $"A pending repair wave supports at most {CandidateLimit} packets.");
        }
        if (!parsedCommand.Success)
        {
            throw new InvalidOperationException(
                "A pending repair root requires one strictly parsed command root.");
        }
        var recomposed = WoundResponseInputComposer.RecomposeCommandRoot(
            binding,
            parsedCommand,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        if (!recomposed.Success)
        {
            throw new InvalidOperationException(
                "The parsed command root does not match the pending accepted-turn binding.");
        }
        var repairPackets = new JsonArray(packetArray
            .Select(static packet => (JsonNode?)packet.ToJsonObject())
            .ToArray());
        var repairReceipts = new JsonArray(packetArray
            .Select(static packet => packet.CreateReceipt())
            .Select(static receipt => (JsonNode?)new JsonObject
            {
                ["sessionId"] = receipt.SessionId,
                ["requestId"] = receipt.RequestId,
                ["snapshotToken"] = receipt.SnapshotToken,
                ["candidateRef"] = receipt.CandidateRef,
                ["semanticFingerprint"] = receipt.SemanticFingerprint
            })
            .ToArray());
        if (packetArray.Length != 0 &&
            !IsValidPersistedRepairWave(
                JsonSerializer.SerializeToElement(repairPackets),
                JsonSerializer.SerializeToElement(repairReceipts),
                binding.SessionId,
                binding.RequestId,
                binding.SnapshotToken))
        {
            throw new InvalidOperationException(
                "The pending repair wave does not match its accepted-turn binding.");
        }
        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["sessionId"] = binding.SessionId,
            ["requestId"] = binding.RequestId,
            ["snapshotToken"] = binding.SnapshotToken,
            ["repairPackets"] = repairPackets,
            ["repairReceipts"] = repairReceipts
        };
        if (packetArray.Length != 0 &&
            parsedCommand.Success &&
            parsedCommand.TreatmentCommands.Count != 0)
        {
            root["submittedTreatmentRequests"] = new JsonArray(
                parsedCommand.TreatmentCommands
                    .Select(static command => (JsonNode?)new JsonObject
                    {
                        ["operationKey"] = command.OperationKey,
                        ["attemptId"] = command.AttemptId,
                        ["requestFingerprint"] = command.RequestFingerprint,
                        ["request"] = command.Request.DeepClone()
                    })
                    .ToArray());
        }
        return root;
    }
}
