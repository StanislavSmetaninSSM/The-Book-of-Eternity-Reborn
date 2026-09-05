using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.Services;

[JsonConverter(typeof(WoundTransitionResultJsonConverter))]
internal sealed class MortalWoundTreatmentPersistedResult : WoundTransitionResult
{
    private readonly JsonObject _canonicalResult;

    private MortalWoundTreatmentPersistedResult(
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentReceipt receipt,
        JsonObject canonicalResult)
    {
        Request = request;
        Receipt = receipt;
        _canonicalResult = canonicalResult.DeepClone().AsObject();
    }

    public MortalWoundTreatmentAttemptRequest Request { get; }
    public MortalWoundTreatmentReceipt Receipt { get; }
    public override string Kind => "treat";
    internal override JsonObject ToCanonicalJson() => _canonicalResult.DeepClone().AsObject();

    internal static MortalWoundTreatmentPersistedResult Create(
        MortalWoundTreatmentResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        var serialized = WoundResponseInputComposer
            .ComposeMortalWoundTreatmentPersistedResult(
                resolution.RequestAuthority,
                MortalWoundTreatmentReceipt.Create(resolution));
        var issues = new List<ValidationIssue>();
        var parsed = Parse(
            JsonSerializer.SerializeToElement(serialized),
            "treatmentResult",
            issues);
        if (parsed is null || issues.Count != 0)
        {
            throw new InvalidOperationException(
                "The resolved treatment did not survive strict persisted-result reconstruction: " +
                string.Join(", ", issues.Select(static issue =>
                    issue.Code + "@" + issue.FilePath)));
        }
        return parsed;
    }

    internal static MortalWoundTreatmentPersistedResult? Parse(
        JsonElement element,
        string path,
        ICollection<ValidationIssue> issues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(issues);
        return MortalWoundTreatmentCommandCodec.TryParseHistoryResult(
            element,
            path,
            issues,
            out var request,
            out var receipt,
            out var canonicalResult) &&
               request is not null &&
               receipt is not null &&
               canonicalResult is not null
            ? new MortalWoundTreatmentPersistedResult(
                request,
                receipt,
                canonicalResult)
            : null;
    }

    internal JsonObject SerializeRequest() =>
        _canonicalResult["requestAuthority"]!.DeepClone().AsObject();

    internal override void WriteCanonical(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _canonicalResult.WriteTo(writer);
    }
}

internal sealed partial class MortalWoundTreatmentReceipt
{
    internal static MortalWoundTreatmentReceipt? RestorePersisted(
        MortalWoundTreatmentAttemptRequest request,
        string resultCategory,
        int? selectedOutcomeIndex,
        bool interruption,
        IReadOnlyList<MortalWoundTreatmentOperation> declaredResult,
        string consumptionTrigger,
        string? courseId,
        int? courseMilestoneOrdinal,
        string? courseDisposition,
        MortalWoundTreatmentModeEvidence modeEvidence,
        string routeFingerprint,
        string resolutionAuthorityFingerprint,
        string resultFingerprint,
        string routeCompletion,
        string receiptFingerprint)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(declaredResult);
        ArgumentNullException.ThrowIfNull(modeEvidence);
        var restored = new MortalWoundTreatmentReceipt(
            request.Mode,
            request.Coordinates.DetachedCopy(),
            "AcceptedTerminal",
            resultCategory,
            selectedOutcomeIndex,
            interruption,
            declaredResult,
            consumptionTrigger,
            courseId,
            courseMilestoneOrdinal,
            courseDisposition,
            request.RequirementAuthority.AuthorityFingerprint,
            request.ResourceAuthority.AuthorityFingerprint,
            DetachEvidence(modeEvidence),
            routeFingerprint,
            resolutionAuthorityFingerprint,
            request.RequestFingerprint,
            resultFingerprint,
            routeCompletion,
            receiptFingerprint);
        return restored.HasMatchingFingerprint() ? restored : null;
    }
}

internal sealed class MortalWoundTreatmentReplayProbeResult
{
    private readonly ReadOnlyCollection<ValidationIssue> _issues;

    private MortalWoundTreatmentReplayProbeResult(
        string status,
        IEnumerable<ValidationIssue> issues,
        MortalWoundTreatmentAttemptRequest? request,
        MortalWoundTreatmentReceipt? receipt)
    {
        Status = status;
        _issues = MortalWoundTreatmentShellDetachment.Freeze(issues);
        Request = request;
        Receipt = receipt;
    }

    public string Status { get; }
    public IReadOnlyList<ValidationIssue> Issues => _issues;
    public MortalWoundTreatmentAttemptRequest? Request { get; }
    public MortalWoundTreatmentReceipt? Receipt { get; }

    internal static MortalWoundTreatmentReplayProbeResult InvalidHistory(
        IEnumerable<ValidationIssue> issues) => new(
        "InvalidHistory",
        issues,
        null,
        null);

    internal static MortalWoundTreatmentReplayProbeResult NotFound() => new(
        "NotFound",
        Array.Empty<ValidationIssue>(),
        null,
        null);

    internal static MortalWoundTreatmentReplayProbeResult ExactReplay(
        MortalWoundTreatmentPersistedResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new MortalWoundTreatmentReplayProbeResult(
            "ExactReplay",
            Array.Empty<ValidationIssue>(),
            result.Request,
            result.Receipt);
    }

    internal static MortalWoundTreatmentReplayProbeResult Conflict(
        string path,
        string expected,
        string actual) => new(
        "Conflict",
        new[]
        {
            new ValidationIssue(
                path,
                IssueSeverity.Error,
                "The Mortal wound-treatment replay coordinates conflict with durable history.",
                code: "wound_history_conflicting_treatment_replay",
                actor: "Client",
                section: "wound_materialization",
                expected: expected,
                actual: actual,
                repairHint:
                "Restore the exact accepted treatment operation, attempt, and request fingerprint.")
        },
        null,
        null);
}

internal sealed partial record WoundHistoryParseResult
{
    internal MortalWoundTreatmentReplayProbeResult ProbeTreatmentAttempt(
        string operationKey,
        string attemptId,
        string requestFingerprint)
    {
        if (!IsValid)
            return MortalWoundTreatmentReplayProbeResult.InvalidHistory(Issues);

        if (!ResourceMaterializationContract.IsExactIdentifier(operationKey) ||
            !ResourceMaterializationContract.IsExactIdentifier(attemptId) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(requestFingerprint))
        {
            return MortalWoundTreatmentReplayProbeResult.Conflict(
                "woundHistory.treatmentReplay",
                "three non-empty treatment replay coordinates",
                "one or more coordinates are empty");
        }

        var related = State!.Transitions.Where(transition =>
                string.Equals(transition.OperationKey, operationKey, StringComparison.Ordinal) ||
                string.Equals(transition.AttemptId, attemptId, StringComparison.Ordinal) ||
                string.Equals(
                    transition.TreatmentResult?.Request.RequestFingerprint,
                    requestFingerprint,
                    StringComparison.Ordinal))
            .ToArray();
        if (related.Length == 0)
            return MortalWoundTreatmentReplayProbeResult.NotFound();

        var exact = related.Where(transition =>
                string.Equals(transition.OperationKey, operationKey, StringComparison.Ordinal) &&
                string.Equals(transition.AttemptId, attemptId, StringComparison.Ordinal) &&
                string.Equals(
                    transition.TreatmentResult?.Request.RequestFingerprint,
                    requestFingerprint,
                    StringComparison.Ordinal) &&
                transition.TreatmentResult is not null)
            .ToArray();
        if (exact.Length == 1 && related.Length == 1)
            return MortalWoundTreatmentReplayProbeResult.ExactReplay(
                exact[0].TreatmentResult!);

        return MortalWoundTreatmentReplayProbeResult.Conflict(
            "woundHistory.transitions",
            "one exact typed treatment request and receipt under all three coordinates",
            $"{related.Length} durable row(s) occupy the operation or attempt coordinate");
    }
}
